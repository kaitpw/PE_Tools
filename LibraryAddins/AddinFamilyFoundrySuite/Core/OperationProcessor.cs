using AddinFamilyFoundrySuite.Core.Aggregators;
using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using PeExtensions.FamDocument;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public class OperationProcessor(
    Document doc,
    ExecutionOptions executionOptions = null,
    IProjectSnapshotCollector projectCollector = null,
    IFamilyDocSnapshotCollector familyDocCollector = null
) : IDisposable {
    private readonly ExecutionOptions _exOpts = executionOptions ?? new ExecutionOptions();
    private readonly IFamilyDocSnapshotCollector _familyDocCollector = familyDocCollector;
    private readonly IProjectSnapshotCollector _projectCollector = projectCollector;

    /// <summary>
    ///     A function to select families in the Document. If the document is a family document, this will not be called
    /// </summary>
    private Func<List<Family>> _documentFamilySelector;

    private Document OpenDoc { get; } = doc;

    public void Dispose() { }

    public OperationProcessor SelectFamilies(params Func<List<Family>>[] familySelectors) {
        var selectorList = familySelectors.ToList();
        if (selectorList == null || selectorList.Count == 0)
            throw new ArgumentException(@"At least one family selector must be provided", nameof(familySelectors));
        this._documentFamilySelector = () => selectorList
            .SelectMany(selector => selector() ?? new List<Family>())
            .GroupBy(f => f.Id)
            .Select(g => g.First())
            .ToList();
        return this;
    }

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling.
    ///     Returns FamilyProcessingContext with pre/post snapshots when a collector is provided.
    /// </summary>
    public (List<FamilyProcessingContext> familyContexts, double totalMs) ProcessQueue(
        OperationQueue queue,
        string outputFolderPath = null,
        LoadAndSaveOptions loadAndSaveOptions = null) {
        var totalSw = Stopwatch.StartNew();

        var contexts = this.OpenDoc.IsFamilyDocument
            ? this.ProcessFamilyDocument(queue)
            : this.ProcessNormalDocument(queue, loadAndSaveOptions, outputFolderPath);

        totalSw.Stop();
        return (contexts, totalSw.Elapsed.TotalMilliseconds);
    }

    public (List<FamilyProcessingContext> familyContexts, double totalMs) ProcessQueueDangerously(
        OperationQueue queue,
        string outputFolderPath = null,
        LoadAndSaveOptions loadAndSaveOptions = null
    ) {
        var totalSw = Stopwatch.StartNew();

        var contexts = this.OpenDoc.IsFamilyDocument
            ? this.ProcessFamilyDocument(queue)
            : this.ProcessNormalDocument(queue, loadAndSaveOptions, outputFolderPath);

        var errors = contexts
            .Where(ctx => {
                var (_, err) = ctx.OperationLogs;
                return err != null;
            }).Select(ctx => {
                var (_, err) = ctx.OperationLogs;
                return err;
            }).ToList();
        if (errors.Any()) throw errors.First();

        totalSw.Stop();
        return (contexts, totalSw.Elapsed.TotalMilliseconds);
    }

    private List<FamilyProcessingContext> ProcessNormalDocument(
        OperationQueue queue,
        LoadAndSaveOptions loadAndSaveOptions,
        string outputFolderPath
    ) {
        var contexts = new List<FamilyProcessingContext>();
        var families = this._documentFamilySelector();
        if (families == null || families.Count == 0) {
            var err = new ArgumentNullException(nameof(families),
                @"There must be families specified for processing if the open document is a normal model document");
            contexts.Add(new FamilyProcessingContext { FamilyName = "ERROR", OperationLogs = err, TotalMs = 0 });
            return contexts;
        }

        foreach (var family in families) {
            var familyName = family.Name;
            var context = new FamilyProcessingContext { FamilyName = familyName };
            var logs = new List<OperationLog>();

            try {
                var familySw = Stopwatch.StartNew();

                // Collect pre-snapshot before EditFamily
                if (this._projectCollector is not null) {
                    var pre = new FamilySnapshot { FamilyName = familyName };
                    this._projectCollector.Collect((this.OpenDoc, family), pre);
                    context.PreProcessSnapshot = pre;
                }

                // Inject context into snapshot-aware operations
                InjectContextIntoOperations(queue, context);

                var familyFuncs = queue.ToFuncs(
                    this._exOpts.OptimizeTypeOperations,
                    this._exOpts.SingleTransaction);

                _ = this.OpenDoc
                    .GetFamilyDocument(family)
                    .EnsureDefaultType()
                    .ProcessWithoutSaving(this.CaptureLogs(familyFuncs, logs))
                    .SaveToLocations(famDoc =>
                        GetSaveLocations(famDoc, loadAndSaveOptions ?? new LoadAndSaveOptions(), outputFolderPath))
                    .LoadAndClose(this.OpenDoc, new EditAndLoadFamilyOptions());

                // Collect post-snapshot after LoadAndClose
                if (this._projectCollector is not null) {
                    var post = new FamilySnapshot { FamilyName = familyName };
                    this._projectCollector.Collect((this.OpenDoc, family), post);
                    context.PostProcessSnapshot = post;
                }

                familySw.Stop();
                context.OperationLogs = logs;
                context.TotalMs = familySw.Elapsed.TotalMilliseconds;
            } catch (Exception ex) {
                context.OperationLogs =
                    new Exception($"Failed to process family {familyName}: {ex.Message}\n{ex.ToStringDemystified()}");
                context.TotalMs = 0;
            }

            contexts.Add(context);
        }

        return contexts;
    }

    private List<FamilyProcessingContext> ProcessFamilyDocument(OperationQueue queue) {
        var context = new FamilyProcessingContext { FamilyName = this.OpenDoc.Title };
        var logs = new List<OperationLog>();

        try {
            var familySw = Stopwatch.StartNew();

            // Inject context for snapshot-aware operations
            InjectContextIntoOperations(queue, context);

            var familyFuncs = queue.ToFuncs(
                this._exOpts.OptimizeTypeOperations,
                this._exOpts.SingleTransaction);

            var famDoc = this.OpenDoc
                .GetFamilyDocument()
                .EnsureDefaultType();

            if (this._familyDocCollector is not null) {
                var pre = new FamilySnapshot { FamilyName = context.FamilyName };
                this._familyDocCollector.Collect(famDoc, pre);
                context.PreProcessSnapshot = pre;
            }

            _ = famDoc.ProcessWithoutSaving(this.CaptureLogs(familyFuncs, logs));

            if (this._familyDocCollector is not null) {
                var post = new FamilySnapshot { FamilyName = context.FamilyName };
                this._familyDocCollector.Collect(famDoc, post);
                context.PostProcessSnapshot = post;
            }

            familySw.Stop();
            context.OperationLogs = logs;
            context.TotalMs = familySw.Elapsed.TotalMilliseconds;
        } catch (Exception ex) {
            context.OperationLogs =
                new Exception(
                    $"Failed to process family {this.OpenDoc.Title}: {ex.Message}\n{ex.ToStringDemystified()}");
            context.TotalMs = 0;
        }

        return [context];
    }

    /// <summary>
    ///     Injects the processing context into context-aware operations and resets all group contexts.
    /// </summary>
    private static void InjectContextIntoOperations(OperationQueue queue, FamilyProcessingContext context) {
        foreach (var op in queue.Operations) {
            switch (op) {
            // Inject family context into operations that need it
            case DocOperationWithContext<IOperationSettings> docWithCtx:
                docWithCtx.Context = context;
                break;
            case TypeOperationWithContext<IOperationSettings> typeWithCtx:
                typeWithCtx.Context = context;
                break;
            }
        }

        // Reset all group contexts per-family (group context is injected by OperationGroup, not here)
        var groupContexts = queue.Operations
            .Select(op => op switch {
                DocOperationWithGroup<IOperationSettings> d => d.GroupContext,
                TypeOperationWithGroup<IOperationSettings> t => t.GroupContext,
                _ => null
            })
            .Where(ctx => ctx != null)
            .Distinct();

        foreach (var ctx in groupContexts)
            ctx.Reset();
    }

    public List<FamilyProcessingContext> ProcessFamilyDocumentIntoVariants(
        List<(string variant, OperationQueue queue)> variants,
        string outputDirectory
    ) {
        var context = new FamilyProcessingContext { FamilyName = this.OpenDoc.Title };
        var logs = new List<OperationLog>();

        try {
            var familySw = Stopwatch.StartNew();

            if (variants == null || variants.Count == 0) return [];
            if (outputDirectory != null && !Directory.Exists(outputDirectory))
                _ = Directory.CreateDirectory(outputDirectory);

            var variantAndCallbacksList = variants
                .Select(v => (suffix: v.variant,
                    this.CaptureLogs(v.queue.ToFuncs(false, false), logs)))
                .ToList();

            foreach (var (variant, callbacks) in variantAndCallbacksList) {
                _ = this.OpenDoc
                    .GetFamilyDocument()
                    .EnsureDefaultType()
                    .ProcessAndSaveVariant(outputDirectory, variant,
                        document => document.ProcessWithoutSaving(callbacks));
            }

            familySw.Stop();
            context.OperationLogs = logs;
            context.TotalMs = familySw.Elapsed.TotalMilliseconds;
        } catch (Exception ex) {
            context.OperationLogs = new Exception($"Failed to process family {this.OpenDoc.Title}: {ex.Message}");
            context.TotalMs = 0;
        }

        return [context];
    }

    private Action<FamilyDocument>[] CaptureLogs(
        Func<FamilyDocument, List<OperationLog>>[] funcActions,
        List<OperationLog> logCollector
    ) => funcActions.Select(func => new Action<FamilyDocument>(famDoc => logCollector.AddRange(func(famDoc))))
        .ToArray();

    private static List<string> GetSaveLocations(FamilyDocument famDoc,
        LoadAndSaveOptions options,
        string outputFolderPath) {
        var saveLocations = new List<string>();
        if ((options?.SaveFamilyToInternalPath ?? false)
            && string.IsNullOrEmpty(outputFolderPath))
            saveLocations.Add(outputFolderPath);

        if (options?.SaveFamilyToOutputDir ?? false) {
            var saveLocation = famDoc.PathName;
            saveLocations.Add(saveLocation);
        }

        return saveLocations;
    }
}

public class ExecutionOptions {
    [Description(
        "When enabled, the command will output a JSON file with all APS parameters and families that would be processed, without actually processing them.")]
    public bool PreviewRun { get; init; } = false;

    [Description("When enabled, the command will bundle the operations into a single transaction.")]
    public bool SingleTransaction { get; init; } = true;

    [Description("When enabled, consecutive type operations will be batched together for better performance.")]
    public bool OptimizeTypeOperations { get; init; } = true;
}

public class LoadAndSaveOptions {
    [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
    [Required]
    public bool OpenOutputFilesOnCommandFinish { get; set; } = true;

    [Description(
        "Load processed family(ies) into the main model document (if the command is run on a main model document)")]
    [Required]
    public bool LoadFamily { get; set; } = true;

    [Description("Save processed family(ies) to the internal path of the family document on your computer")]
    [Required]
    public bool SaveFamilyToInternalPath { get; set; } = false;

    [Description("Save processed family(ies) to the output directory of the command")]
    [Required]
    public bool SaveFamilyToOutputDir { get; set; } = false;
}

internal class EditAndLoadFamilyOptions : IFamilyLoadOptions {
    public bool OnFamilyFound(
        bool familyInUse,
        out bool overwriteParameterValues) {
        overwriteParameterValues = true;
        return true;
    }

    public bool OnSharedFamilyFound(
        Family sharedFamily,
        bool familyInUse,
        out FamilySource source,
        out bool overwriteParameterValues) {
        source = FamilySource.Project;
        overwriteParameterValues = true;
        return true;
    }
}