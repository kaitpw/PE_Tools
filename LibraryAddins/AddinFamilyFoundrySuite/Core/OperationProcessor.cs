using PeExtensions.FamDocument;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public record FamilyProcessOutput(string FamilyName, Result<List<OperationLog>> Logs, double TotalMs);

public class OperationProcessor(
    Document doc,
    ExecutionOptions executionOptions = null
) : IDisposable {
    private readonly ExecutionOptions _exOpts = executionOptions ?? new ExecutionOptions();

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
    ///     Execution options (preview run, single transaction, optimize type operations) are controlled by
    ///     ExecutionOptions.
    /// </summary>
    public (List<FamilyProcessOutput> familyResults, double totalMs) ProcessQueue(
        OperationQueue queue,
        string outputFolderPath = null,
        LoadAndSaveOptions loadAndSaveOptions = null) {
        var familyResults = new List<FamilyProcessOutput>();
        var totalSw = Stopwatch.StartNew();

        var familyFuncs = queue.ToFuncs(
            this._exOpts.OptimizeTypeOperations,
            this._exOpts.SingleTransaction);
        var outputs = this.OpenDoc.IsFamilyDocument
            ? this.ProcessFamilyDocument(familyFuncs)
            : this.ProcessNormalDocument(familyFuncs, loadAndSaveOptions, outputFolderPath);

        familyResults.AddRange(outputs);
        totalSw.Stop();
        return (familyResults, totalSw.Elapsed.TotalMilliseconds);
    }

    public (List<FamilyProcessOutput> familyResults, double totalMs) ProcessQueueDangerously(
        OperationQueue queue,
        string outputFolderPath = null,
        LoadAndSaveOptions loadAndSaveOptions = null
    ) {
        var familyResults = new List<FamilyProcessOutput>();
        var totalSw = Stopwatch.StartNew();

        var familyFuncs = queue.ToFuncs(
            this._exOpts.OptimizeTypeOperations,
            this._exOpts.SingleTransaction);
        var outputs = this.OpenDoc.IsFamilyDocument
            ? this.ProcessFamilyDocument(familyFuncs)
            : this.ProcessNormalDocument(familyFuncs, loadAndSaveOptions, outputFolderPath);
        var errors = outputs
            .Where(output => {
                var (_, err) = output.Logs;
                return err != null;
            }).Select(output => {
                var (_, err) = output.Logs;
                return err;
            }).ToList();
        if (errors.Any()) throw errors.First();

        familyResults.AddRange(outputs);
        totalSw.Stop();
        return (familyResults, totalSw.Elapsed.TotalMilliseconds);
    }

    private List<FamilyProcessOutput> ProcessNormalDocument(
        Func<FamilyDocument, List<OperationLog>>[] familyFuncs,
        LoadAndSaveOptions loadAndSaveOptions,
        string outputFolderPath
    ) {
        var familyResults = new List<FamilyProcessOutput>();
        var families = this._documentFamilySelector();
        if (families == null || families.Count == 0) {
            var err = new ArgumentNullException(nameof(families),
                @"There must be families specified for processing if the open document is a normal model document");
            familyResults.Add(new FamilyProcessOutput("ERROR", err, 0));
            return familyResults;
        }

        foreach (var family in families) {
            var familyName = family.Name; // Capture name 
            var logs = new List<OperationLog>();
            try {
                var familySw = Stopwatch.StartNew();
                _ = this.OpenDoc
                    .GetFamilyDocument(family)
                    .EnsureDefaultType()
                    .ProcessWithoutSaving(this.CaptureLogs(familyFuncs, logs))
                    .SaveToLocations(famDoc =>
                        GetSaveLocations(famDoc, loadAndSaveOptions ?? new LoadAndSaveOptions(), outputFolderPath))
                    .LoadAndClose(this.OpenDoc, new EditAndLoadFamilyOptions());
                familySw.Stop();
                familyResults.Add(new FamilyProcessOutput(familyName, logs, familySw.Elapsed.TotalMilliseconds));
            } catch (Exception ex) {
                familyResults.Add(new FamilyProcessOutput(
                    familyName,
                    new Exception($"Failed to process family {familyName}: {ex.Message}"),
                    0));
            }
        }

        return familyResults;
    }


    private List<FamilyProcessOutput> ProcessFamilyDocument(
        Func<FamilyDocument, List<OperationLog>>[] familyFuncs
    ) {
        var logs = new List<OperationLog>();
        try {
            var familySw = Stopwatch.StartNew();
            _ = this.OpenDoc
                .GetFamilyDocument()
                .EnsureDefaultType()
                .ProcessWithoutSaving(this.CaptureLogs(familyFuncs, logs));
            familySw.Stop();
            return new List<FamilyProcessOutput> { new(this.OpenDoc.Title, logs, familySw.Elapsed.TotalMilliseconds) };
        } catch (Exception ex) {
            return new List<FamilyProcessOutput> {
                new(
                    this.OpenDoc.Title,
                    new Exception($"Failed to process family {this.OpenDoc.Title}: {ex.Message}"),
                    0)
            };
        }
    }

    public List<FamilyProcessOutput> ProcessFamilyDocumentIntoVariants(
        List<(string variant, OperationQueue queue)> variants,
        string outputDirectory
    ) {
        var logs = new List<OperationLog>();
        try {
            var familySw = Stopwatch.StartNew();

            if (variants == null || variants.Count == 0) return new List<FamilyProcessOutput>();
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
            return new List<FamilyProcessOutput> { new(this.OpenDoc.Title, logs, familySw.Elapsed.TotalMilliseconds) };
        } catch (Exception ex) {
            return new List<FamilyProcessOutput> {
                new(
                    this.OpenDoc.Title,
                    new Exception($"Failed to process family {this.OpenDoc.Title}: {ex.Message}"),
                    0)
            };
        }
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