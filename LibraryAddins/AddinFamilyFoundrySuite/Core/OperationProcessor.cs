using PeExtensions.FamDocument;
using PeServices.Storage;
using PeUtils.Files;

namespace AddinFamilyFoundrySuite.Core;

public class OperationProcessor<TProfile>
    where TProfile : BaseProfileSettings, new() {
    public OperationProcessor(Storage storage) {
        this.storage = storage;
        this.settings = this.storage.Settings().Json<BaseSettings<TProfile>>().Read();
        this.profile = this.settings.GetProfile();
    }

    public Storage storage { get; }
    public BaseSettings<TProfile> settings { get; }
    public TProfile profile { get; }

    public OperationQueue<TProfile> CreateQueue() => new(this.profile);

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling
    /// </summary>
    public List<OperationLog> ProcessQueue(Document doc, OperationQueue<TProfile> enqueuer) {
        var allFamilyLogs = new Dictionary<string, List<OperationLog>>();

        if (doc.IsFamilyDocument) {
            var (familyActions, getLogs) = enqueuer.ToFamilyActions();
            try {
                var saveLocation = this.GetSaveLocations(doc, this.settings.OnProcessingFinish);
                _ = doc
                    .ProcessFamily(familyActions)
                    .SaveFamily(saveLocation);
                allFamilyLogs.Add(doc.Title, getLogs());
            } catch (Exception ex) {
                Debug.WriteLine($"Failed to process family {doc.Title}: {ex.Message}");
            }
        } else {
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(this.profile.FilterFamilies.Filter)
                .ToList();

            foreach (var family in families) {
                var familyName = family.Name; // Capture name before processing as family object becomes invalid after LoadAndCloseFamily
                var (familyActions, getLogs) = enqueuer.ToFamilyActions();
                try {
                    var saveLocation = this.GetSaveLocations(doc, this.settings.OnProcessingFinish);
                    _ = doc
                        .EditFamily(family)
                        .ProcessFamily(familyActions)
                        .SaveFamily(saveLocation)
                        .LoadAndCloseFamily(doc, new EditAndLoadFamilyOptions());
                    allFamilyLogs.Add(familyName, getLogs());
                } catch (Exception ex) {
                    Debug.WriteLine($"Failed to process family {familyName}: {ex.Message}");
                }
            }
        }

        var logPath = this.WriteLogs(allFamilyLogs);
        if (this.settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish) {
            FileUtils.OpenInDefaultApp(logPath);
        }
        return allFamilyLogs.SelectMany(kvp => kvp.Value).ToList();
    }

    private string WriteLogs(Dictionary<string, List<OperationLog>> familyLogs) {
        var logData = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ProcessedFamilies = familyLogs.Select(kvp => new {
                FamilyName = kvp.Key,
                Operations = kvp.Value.Select(log => {
                    var result = new Dictionary<string, object> {
                        ["OperationName"] = log.OperationName,
                        ["SuccessCount"] = log.SuccessCount,
                        ["FailedCount"] = log.FailedCount,
                        ["Errors"] = log.Entries
                            .Where(e => e.Error != null)
                            .Select(e => e.Context != null ? $"[{e.Context}] {e.Item}: {e.Error}" : $"{e.Item}: {e.Error}")
                            .ToList(),
                        ["SecondsTotalElapsed"] = Math.Round(log.MsTotalElapsed / 1000.0, 3)
                    };

                    if (log.MsAvgPerType.HasValue) {
                        result["SecondsAvgPerType"] = Math.Round(log.MsAvgPerType.Value / 1000.0, 3);
                    }

                    return result;
                }).ToList()
            }).ToList()
        };

        var filename = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
        this.storage.Output().Json<object>(filename).Write(logData);
        return Path.Combine(this.storage.Output().GetFolderPath(), filename);
    }

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