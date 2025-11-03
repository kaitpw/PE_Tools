using PeExtensions.FamDocument;
using PeRevit.Lib;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using static AddinFamilyFoundrySuite.Core.BaseProfileSettings;

namespace AddinFamilyFoundrySuite.Core;

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
        if (selectorList == null || selectorList.Count == 0) {
            throw new ArgumentException("At least one family selector must be provided", nameof(familySelectors));
        }
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
    public (Dictionary<string, (List<OperationLog>, double)> familyResults, double totalMs) ProcessQueue(
        OperationQueue queue,
        string outputFolderPath = null,
        LoadAndSaveOptions loadAndSaveOptions = null) {
        var familyResults = new Dictionary<string, (List<OperationLog> logs, double totalMs)>();
        var totalSw = Stopwatch.StartNew();

        var familyFuncs = queue.ToFuncs(
            this._executionOptions.OptimizeTypeOperations,
            this._executionOptions.SingleTransaction);

        if (this._openDoc.IsFamilyDocument) {
            var logs = new List<OperationLog>();
            try {
                var familySw = Stopwatch.StartNew();
                _ = this._openDoc
                    .GetFamilyDocument()
                    .ProcessFamily(this.CaptureLogs(familyFuncs, logs));
                familySw.Stop();
                familyResults.Add(this._openDoc.Title, (logs, familySw.Elapsed.TotalMilliseconds));
            } catch (Exception ex) {
                Debug.WriteLine($"Failed to process family {this._openDoc.Title}: {ex.Message}");
            }
        } else {
            var families = this._documentFamilySelector();
            if (families == null) {
                throw new ArgumentNullException(nameof(families),
                "There must be families specified for processing"
                + " if the open document is a normal model document");
            }

            foreach (var family in families) {
                var familyName = family.Name; // Capture name 
                var logs = new List<OperationLog>();
                try {
                    var familySw = Stopwatch.StartNew();
                    _ = this._openDoc
                        .GetFamilyDocument(family)
                        .ProcessFamily(this.CaptureLogs(familyFuncs, logs))
                        .SaveFamily(famDoc =>
                            this.GetSaveLocations(famDoc, loadAndSaveOptions ?? new LoadAndSaveOptions(), outputFolderPath))
                        .LoadAndCloseFamily(this._openDoc, new EditAndLoadFamilyOptions());
                    familySw.Stop();
                    familyResults.Add(familyName, (logs, familySw.Elapsed.TotalMilliseconds));
                } catch (Exception ex) {
                    Debug.WriteLine($"Failed to process family {familyName}: {ex.Message}");
                }
            }
        }

        totalSw.Stop();

        return (familyResults, totalSw.Elapsed.TotalMilliseconds);
    }

    public (List<OperationLog> logs, double totalMs) SandboxProcessQueue(
        OperationQueue queue
    ) {
        var familySw = Stopwatch.StartNew();
        var logs = new List<OperationLog>();
        var familyFuncs = queue.ToFuncs(
            this._executionOptions.OptimizeTypeOperations,
            this._executionOptions.SingleTransaction);

        _ = this._openDoc
            .GetFamilyDocument()
            .ProcessFamily(this.CaptureLogs(familyFuncs, logs));

        familySw.Stop();
        return (logs, familySw.Elapsed.TotalMilliseconds);
    }

    private Action<FamilyDocument>[] CaptureLogs(
        Func<FamilyDocument, List<OperationLog>>[] funcActions,
        List<OperationLog> logCollector
    ) => funcActions.Select(func => new Action<FamilyDocument>(famDoc => logCollector.AddRange(func(famDoc)))).ToArray();

    private List<string> GetSaveLocations(FamilyDocument famDoc, LoadAndSaveOptions options, string outputFolderPath) {
        var saveLocations = new List<string>();
        if ((options?.SaveFamilyToInternalPath ?? false)
            && string.IsNullOrEmpty(outputFolderPath)) {
            saveLocations.Add(outputFolderPath);
        }

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

    [Description("When enabled, the command will get the state of certain relevant data in a family.")]
    public string Mode { get; set; } = "";
}
public class LoadAndSaveOptions {
    /// <summary>
    ///     Load the family into the main model document
    /// </summary>
    public bool LoadFamily { get; set; } = true;

    /// <summary>
    ///     Save the family to the internal path of the family document
    /// </summary>
    public bool SaveFamilyToInternalPath { get; set; } = false;

    /// <summary>
    ///     Save the family to the output directory of the command
    /// </summary>
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