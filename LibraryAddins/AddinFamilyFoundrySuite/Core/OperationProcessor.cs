using PeExtensions.FamDocument;
using PeServices.Storage;
using PeUtils.Files;

namespace AddinFamilyFoundrySuite.Core;

public class OperationProcessor<TProfile> : IDisposable
    where TProfile : BaseProfileSettings, new() {
    private TProfile _profile;
    private TempSharedParamFile _tempFile;

    public OperationProcessor(Document doc, Storage storage) {
        this.doc = doc;
        this.storage = storage;
        var settingsManager = this.storage.Settings();
        this.settings = settingsManager.Json<BaseSettings<TProfile>>().Read();
    }

    public Document doc { get; }
    public Storage storage { get; }
    public BaseSettings<TProfile> settings { get; }

    /// <summary>
    ///     Gets the current profile, loading it from storage if not already loaded
    /// </summary>
    public TProfile Profile {
        get {
            this._profile ??= this.settings.GetProfile(this.storage.Settings());
            return this._profile;
        }
    }

    public void Dispose() => this._tempFile?.Dispose();

    /// <summary>
    ///     Gets the APS parameters using a temporary shared parameter file.
    ///     The temp file is disposed when the processor is disposed or when this method is called again.
    /// </summary>
    public List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> GetApsParams() {
        this._tempFile?.Dispose();
        this._tempFile = new TempSharedParamFile(this.doc);
        return this.Profile.GetAPSParams(this._tempFile);
    }

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling.
    ///     Execution options (preview run, single transaction, optimize type operations) are controlled by
    ///     Profile.ExecutionOptions.
    /// </summary>
    public (Dictionary<string, (List<OperationLog>, double)> familyResults, double totalMs) ProcessQueue(
        OperationQueue queue) {
        var familyResults = new Dictionary<string, (List<OperationLog> logs, double totalMs)>();
        var totalSw = Stopwatch.StartNew();

        var familyActions = queue.ToFamilyActions(
            this.Profile.ExecutionOptions.OptimizeTypeOperations,
            this.Profile.ExecutionOptions.SingleTransaction);

        if (this.doc.IsFamilyDocument) {
            var logs = new List<OperationLog>();
            try {
                var familySw = Stopwatch.StartNew();
                var saveLocation = this.GetSaveLocations(this.doc, this.settings.OnProcessingFinish);
                _ = this.doc
                    .ProcessFamily(this.CaptureActionLogs(familyActions, logs))
                    .SaveFamily(saveLocation);
                familySw.Stop();
                familyResults.Add(this.doc.Title, (logs, familySw.Elapsed.TotalMilliseconds));
            } catch (Exception ex) {
                Debug.WriteLine($"Failed to process family {this.doc.Title}: {ex.Message}");
            }
        } else {
            var families = this.Profile.GetFamilies(this.doc);
            foreach (var family in families) {
                var familyName = family.Name; // Capture name 
                var logs = new List<OperationLog>();
                try {
                    var familySw = Stopwatch.StartNew();
                    var saveLocation = this.GetSaveLocations(this.doc, this.settings.OnProcessingFinish);
                    _ = this.doc
                        .EditFamily(family)
                        .ProcessFamily(this.CaptureActionLogs(familyActions, logs))
                        .SaveFamily(saveLocation)
                        .LoadAndCloseFamily(this.doc, new EditAndLoadFamilyOptions());
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

    private Action<Document>[] CaptureActionLogs(
        Func<Document, List<OperationLog>>[] funcActions,
        List<OperationLog> logCollector
    ) => funcActions.Select(func => new Action<Document>(famDoc => logCollector.AddRange(func(famDoc)))).ToArray();

    private List<string> GetSaveLocations(Document famDoc, ILoadAndSaveOptions options) {
        var saveLocations = new List<string>();
        if (options.SaveFamilyToInternalPath) {
            var saveLocation = this.storage.Output().GetFolderPath();
            saveLocations.Add(saveLocation);
        }

        if (options.SaveFamilyToOutputDir) {
            var saveLocation = famDoc.PathName;
            saveLocations.Add(saveLocation);
        }

        return saveLocations;
    }
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