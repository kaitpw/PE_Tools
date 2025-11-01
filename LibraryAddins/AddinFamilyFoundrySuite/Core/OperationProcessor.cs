using PeExtensions.FamDocument;
using PeUtils.Files;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using static AddinFamilyFoundrySuite.Core.BaseProfileSettings;

namespace AddinFamilyFoundrySuite.Core;

public class OperationProcessor<TProfile> : IDisposable
    where TProfile : BaseProfileSettings, new() {
    private TempSharedParamFile _tempFile;
    private readonly Func<Document, List<Family>> _getFamilies;
    private readonly Func<TempSharedParamFile, List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>> _getApsParams;
    private readonly ExecutionOptions _executionOptions;

    public OperationProcessor(
        Document doc,
        Func<Document, List<Family>> getFamilies,
        Func<TempSharedParamFile, List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>> getApsParams,
        ExecutionOptions executionOptions) {
        this._openDoc = doc;
        this._getFamilies = getFamilies;
        this._getApsParams = getApsParams;
        this._executionOptions = executionOptions;
    }

    private Document _openDoc { get; }

    public void Dispose() => this._tempFile?.Dispose();

    /// <summary>
    ///     Gets the APS parameters using a temporary shared parameter file.
    ///     The temp file is disposed when the processor is disposed or when this method is called again.
    /// </summary>
    public List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> GetApsParams() {
        this._tempFile?.Dispose();
        this._tempFile = new TempSharedParamFile(this._openDoc);
        return this._getApsParams(this._tempFile);
    }

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling.
    ///     Execution options (preview run, single transaction, optimize type operations) are controlled by
    ///     ExecutionOptions.
    /// </summary>
    public (Dictionary<string, (List<OperationLog>, double)> familyResults, double totalMs) ProcessQueue(
        OperationQueue queue,
        string outputFolderPath,
        ILoadAndSaveOptions loadAndSaveOptions) {
        var familyResults = new Dictionary<string, (List<OperationLog> logs, double totalMs)>();
        var totalSw = Stopwatch.StartNew();

        var familyFuncs = queue.ToFuncs(
            this._executionOptions.OptimizeTypeOperations,
            this._executionOptions.SingleTransaction);

        if (this._openDoc.IsFamilyDocument) {
            var logs = new List<OperationLog>();
            try {
                var familySw = Stopwatch.StartNew();
                _ = new FamilyDocument(this._openDoc)
                    .ProcessFamily(this.CaptureLogs(familyFuncs, logs))
                    .SaveFamily(famDoc => this.GetSaveLocations(famDoc, loadAndSaveOptions, outputFolderPath));
                familySw.Stop();
                familyResults.Add(this._openDoc.Title, (logs, familySw.Elapsed.TotalMilliseconds));
            } catch (Exception ex) {
                Debug.WriteLine($"Failed to process family {this._openDoc.Title}: {ex.Message}");
            }
        } else {
            var families = this._getFamilies(this._openDoc);
            foreach (var family in families) {
                var familyName = family.Name; // Capture name 
                var logs = new List<OperationLog>();
                try {
                    var familySw = Stopwatch.StartNew();
                    _ = this._openDoc
                        .GetFamily(family)
                        .ProcessFamily(this.CaptureLogs(familyFuncs, logs))
                        .SaveFamily(famDoc => this.GetSaveLocations(famDoc, loadAndSaveOptions, outputFolderPath))
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

    private Action<FamilyDocument>[] CaptureLogs(
        Func<FamilyDocument, List<OperationLog>>[] funcActions,
        List<OperationLog> logCollector
    ) => funcActions.Select(func => new Action<FamilyDocument>(famDoc => logCollector.AddRange(func(famDoc)))).ToArray();

    private List<string> GetSaveLocations(FamilyDocument famDoc, ILoadAndSaveOptions options, string outputFolderPath) {
        var saveLocations = new List<string>();
        if (options.SaveFamilyToInternalPath) {
            saveLocations.Add(outputFolderPath);
        }

        if (options.SaveFamilyToOutputDir) {
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
public interface ILoadAndSaveOptions {
    /// <summary>
    ///     Load the family into the main model document
    /// </summary>
    bool LoadFamily { get; set; }

    /// <summary>
    ///     Save the family to the internal path of the family document
    /// </summary>
    bool SaveFamilyToInternalPath { get; set; }

    /// <summary>
    ///     Save the family to the output directory of the command
    /// </summary>
    bool SaveFamilyToOutputDir { get; set; }
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