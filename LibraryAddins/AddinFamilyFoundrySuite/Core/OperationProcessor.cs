using PeExtensions.FamDocument;
using PeServices.Storage;

namespace AddinFamilyFoundrySuite.Core;

public class OperationProcessor<TProfile>
    where TProfile : BaseProfileSettings, new() {
    public OperationProcessor(Storage storage) {
        this.storage = storage;
        var settingsManager = this.storage.Settings();
        this.settings = settingsManager.Json<BaseSettings<TProfile>>().Read();
        this.profile = this.settings.GetProfile(settingsManager);
    }

    public Storage storage { get; }
    public BaseSettings<TProfile> settings { get; }
    public TProfile profile { get; }

    public OperationQueue<TProfile> CreateQueue() => new(this.profile);

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling
    /// </summary>
    public List<OperationLog> ProcessQueue(Document doc, OperationQueue<TProfile> queue) {
        if (this.settings.DryRun) {
            this.OutputDryRunResults(doc, queue);
            return new List<OperationLog>();
        }

        var familyResults = new Dictionary<string, (List<OperationLog> logs, double totalMs)>();

        var totalSw = Stopwatch.StartNew();

        var (familyActions, getLogs) = queue.ToFamilyActions();
        if (doc.IsFamilyDocument) {
            try {
                var familySw = Stopwatch.StartNew();
                var saveLocation = this.GetSaveLocations(doc, this.settings.OnProcessingFinish);
                _ = doc
                    .ProcessFamily(familyActions)
                    .SaveFamily(saveLocation);
                familySw.Stop();
                familyResults.Add(doc.Title, (getLogs(), familySw.Elapsed.TotalMilliseconds));
            } catch (Exception ex) {
                Debug.WriteLine($"Failed to process family {doc.Title}: {ex.Message}");
            }
        } else {
            var families = this.profile.GetFamilies(doc);
            foreach (var family in families) {
                var familyName = family.Name; // Capture name 
                try {
                    var familySw = Stopwatch.StartNew();
                    var saveLocation = this.GetSaveLocations(doc, this.settings.OnProcessingFinish);
                    _ = doc
                        .EditFamily(family)
                        .ProcessFamily(familyActions)
                        .SaveFamily(saveLocation)
                        .LoadAndCloseFamily(doc, new EditAndLoadFamilyOptions());
                    familySw.Stop();
                    familyResults.Add(familyName, (getLogs(), familySw.Elapsed.TotalMilliseconds));
                } catch (Exception ex) {
                    Debug.WriteLine($"Failed to process family {familyName}: {ex.Message}");
                }
            }
        }

        totalSw.Stop();

        _ = this.OutputProcessingResults(familyResults, totalSw.Elapsed.TotalMilliseconds);
        return familyResults.SelectMany(kvp => kvp.Value.logs).ToList();
    }

    private void OutputDryRunResults(Document doc, OperationQueue<TProfile> queue) =>
        OperationLogger.OutputDryRunResults(
            this.storage, this.profile, this.settings.CurrentProfile, doc, queue,
            this.settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish
        );

    private string OutputProcessingResults(Dictionary<string, (List<OperationLog> logs, double totalMs)> familyResults,
        double totalMs) =>
        OperationLogger.OutputProcessingResults(
            this.storage, familyResults, totalMs,
            this.settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish
        );

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