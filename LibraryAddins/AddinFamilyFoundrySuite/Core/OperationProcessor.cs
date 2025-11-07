using PeExtensions.FamDocument;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core;

public record FamilyProcessOutput(string familyName, Result<List<OperationLog>> logs, double totalMs);

public class OperationProcessor : IDisposable {
    private readonly ExecutionOptions _executionOptions;

    /// <summary>
    ///     A function to select families in the Document. If the document is a family document, this will not be called
    /// </summary>
    private Func<List<Family>> _documentFamilySelector;

    public OperationProcessor(
        Document doc,
        ExecutionOptions executionOptions = null
    ) {
        this._openDoc = doc;
        this._executionOptions = executionOptions ?? new ExecutionOptions();
    }

    private Document _openDoc { get; }

    public void Dispose() { }

    public OperationProcessor SelectFamilies(params Func<List<Family>>[] familySelectors) {
        var selectorList = familySelectors.ToList();
        if (selectorList == null || selectorList.Count == 0)
            throw new ArgumentException("At least one family selector must be provided", nameof(familySelectors));
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
            this._executionOptions.OptimizeTypeOperations,
            this._executionOptions.SingleTransaction);

        if (this._openDoc.IsFamilyDocument) {
            var outputs = this.ProcessFamilyDocument(familyFuncs);
            familyResults.AddRange(outputs);
        } else {
            var outputs = this.ProcessNormalDocument(outputFolderPath, loadAndSaveOptions, familyFuncs);
            familyResults.AddRange(outputs);
        }

        totalSw.Stop();

        return (familyResults, totalSw.Elapsed.TotalMilliseconds);
    }

    public (List<FamilyProcessOutput> familyResults, double totalMs) SandboxProcessQueue(
        OperationQueue queue,
        string outputFolderPath = null,
        LoadAndSaveOptions loadAndSaveOptions = null
    ) {
        var familyResults = new List<FamilyProcessOutput>();
        var totalSw = Stopwatch.StartNew();

        var familyFuncs = queue.ToFuncs(
            this._executionOptions.OptimizeTypeOperations,
            this._executionOptions.SingleTransaction);
        var outputs = this._openDoc.IsFamilyDocument
            ? this.ProcessFamilyDocument(familyFuncs)
            : this.ProcessNormalDocument(outputFolderPath, loadAndSaveOptions, familyFuncs);
        var errors = outputs
            .Where(output => {
                var (log, err) = output.logs;
                return err != null;
            }).Select(output => {
                var (log, err) = output.logs;
                return err;
            }).ToList();
        if (errors.Any()) throw errors.First();

        familyResults.AddRange(outputs);

        totalSw.Stop();

        return (familyResults, totalSw.Elapsed.TotalMilliseconds);
    }

    private List<FamilyProcessOutput> ProcessNormalDocument(string outputFolderPath,
        LoadAndSaveOptions loadAndSaveOptions,
        Func<FamilyDocument, List<OperationLog>>[] familyFuncs) {
        var familyResults = new List<FamilyProcessOutput>();
        var families = this._documentFamilySelector();
        if (families == null || families.Count == 0) {
            familyResults.Add(new FamilyProcessOutput(
                "ERROR",
                new ArgumentNullException(nameof(families),
                    "There must be families specified for processing"
                    + " if the open document is a normal model document"),
                0));
            return familyResults;
        }

        foreach (var family in families) {
            var familyName = family.Name; // Capture name 
            var logs = new List<OperationLog>();
            try {
                var familySw = Stopwatch.StartNew();
                _ = this._openDoc
                    .GetFamilyDocument(family)
                    .EnsureDefaultType()
                    .ProcessFamily(this.CaptureLogs(familyFuncs, logs))
                    .SaveFamily(famDoc =>
                        this.GetSaveLocations(famDoc, loadAndSaveOptions ?? new LoadAndSaveOptions(), outputFolderPath))
                    .LoadAndCloseFamily(this._openDoc, new EditAndLoadFamilyOptions());
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


    private List<FamilyProcessOutput> ProcessFamilyDocument(Func<FamilyDocument, List<OperationLog>>[] familyFuncs) {
        var logs = new List<OperationLog>();
        try {
            var familySw = Stopwatch.StartNew();
            _ = this._openDoc
                .GetFamilyDocument()
                .EnsureDefaultType()
                .ProcessFamily(this.CaptureLogs(familyFuncs, logs));
            familySw.Stop();
            return new List<FamilyProcessOutput> { new(this._openDoc.Title, logs, familySw.Elapsed.TotalMilliseconds) };
        } catch (Exception ex) {
            return new List<FamilyProcessOutput> {
                new(
                    this._openDoc.Title,
                    new Exception($"Failed to process family {this._openDoc.Title}: {ex.Message}"),
                    0)
            };
        }
    }


    private Action<FamilyDocument>[] CaptureLogs(
        Func<FamilyDocument, List<OperationLog>>[] funcActions,
        List<OperationLog> logCollector
    ) => funcActions.Select(func => new Action<FamilyDocument>(famDoc => logCollector.AddRange(func(famDoc))))
        .ToArray();

    private List<string> GetSaveLocations(FamilyDocument famDoc, LoadAndSaveOptions options, string outputFolderPath) {
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
    public bool PreviewRun { get; set; } = false;

    [Description("When enabled, the command will bundle the operations into a single transaction.")]
    public bool SingleTransaction { get; set; } = true;

    [Description("When enabled, consecutive type operations will be batched together for better performance.")]
    public bool OptimizeTypeOperations { get; set; } = true;
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