using PeExtensions.FamDocument;
using PeServices.Storage;
using PeUtils.Files;

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

        var logPath = this.WriteLogs(familyResults, totalSw.Elapsed.TotalMilliseconds);
        if (this.settings.OnProcessingFinish.OpenOutputFilesOnCommandFinish) FileUtils.OpenInDefaultApp(logPath);
        return familyResults.SelectMany(kvp => kvp.Value.logs).ToList();
    }

    private string WriteLogs(Dictionary<string, (List<OperationLog> logs, double totalMs)> familyResults,
        double totalMs) {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        // Summary log with grouped errors
        var logData = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            TotalSecondsElapsed = Math.Round(totalMs / 1000.0, 3),
            ProcessedFamilies = familyResults.Select(kvp => new {
                FamilyName = kvp.Key,
                TotalSecondsElapsed = Math.Round(kvp.Value.totalMs / 1000.0, 3),
                Operations = kvp.Value.logs.Select(log => {
                    // Group errors by item and error message, collecting contexts
                    var groupedErrors = log.Entries
                        .Where(e => e.Error != null)
                        .GroupBy(e => new { e.Item, e.Error })
                        .Select(g => {
                            var contexts = g.Select(e => e.Context).Where(c => c != null).ToList();
                            var contextsStr = contexts.Any() ? $"[{string.Join(", ", contexts)}] " : "";
                            return $"{contextsStr}{g.Key.Item} : {g.Key.Error}";
                        })
                        .ToList();

                    return new Dictionary<string, object> {
                        ["OperationName"] = log.OperationName,
                        ["SecondsElapsed"] = Math.Round(log.MsElapsed / 1000.0, 3),
                        ["SuccessCount"] = log.SuccessCount,
                        ["FailedCount"] = log.FailedCount,
                        ["Errors"] = groupedErrors,
                    };
                }).ToList()
            }).ToList()
        };

        // Detailed log with all entries
        var detailedLogData = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ProcessedFamilies = familyResults.Select(kvp => new {
                FamilyName = kvp.Key,
                Operations = kvp.Value.logs.Select(log => new {
                    log.OperationName,
                    Successes = log.Entries.Where(e => e.Error == null)
                        .GroupBy(e => new { e.Item, e.Error })
                        .Select(g => {
                            var contexts = g.Select(e => e.Context).Where(c => c != null).ToList();
                            var contextsStr = contexts.Any() ? $"[{string.Join(", ", contexts)}] " : "";
                            return $"{contextsStr}{g.Key.Item}";
                        })
                        .ToList(),
                    Errors = log.Entries
                        .Where(e => e.Error != null)
                        .GroupBy(e => new { e.Item, e.Error })
                        .Select(g => {
                            var contexts = g.Select(e => e.Context).Where(c => c != null).ToList();
                            var contextsStr = contexts.Any() ? $"[{string.Join(", ", contexts)}] " : "";
                            return $"{contextsStr}{g.Key.Item} : {g.Key.Error}";
                        })
                        .ToList()
                }).ToList()
            }).ToList()
        };

        var filename = $"{timestamp}.json";
        var detailedFilename = $"{timestamp}_detailed.json";
        this.storage.Output().Json<object>(filename).Write(logData);
        this.storage.Output().Json<object>(detailedFilename).Write(detailedLogData);
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