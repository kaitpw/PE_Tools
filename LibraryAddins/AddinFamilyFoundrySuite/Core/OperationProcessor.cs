using PeExtensions.FamDocument;
using PeServices.Storage;

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
        var (familyActions, getLogs) = enqueuer.ToFamilyActions();

        if (doc.IsFamilyDocument) {
            try {
                var saveLocation = this.GetSaveLocations(doc, this.settings.OnProcessingFinish);
                _ = doc
                    .ProcessFamily(familyActions)
                    .SaveFamily(saveLocation);
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
                try {
                    var saveLocation = this.GetSaveLocations(doc, this.settings.OnProcessingFinish);
                    _ = doc
                        .EditFamily(family)
                        .ProcessFamily(familyActions)
                        .SaveFamily(saveLocation)
                        .LoadAndCloseFamily(doc, new EditAndLoadFamilyOptions());
                } catch (Exception ex) {
                    Debug.WriteLine($"Failed to process family {family.Name}: {ex.Message}");
                }
            }
        }

        var logs = getLogs();
        this.WriteLogs(logs);
        return logs;
    }

    private void WriteLogs(List<OperationLog> logs) {
        var logData = new {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Operations = logs.Select(log => {
                var result = new Dictionary<string, object> {
                    ["OperationName"] = log.OperationName,
                    ["SuccessCount"] = log.SuccessCount,
                    ["FailedCount"] = log.FailedCount,
                    ["Errors"] = log.Entries
                        .Where(e => e.Error != null)
                        .Select(e => e.Context != null ? $"[{e.Context.Name}] {e.Item}: {e.Error}" : $"{e.Item}: {e.Error}")
                        .ToList(),
                    ["SecondsTotalElapsed"] = Math.Round(log.MsTotalElapsed / 1000.0, 3),
                };

                if (log.MsAvgPerType.HasValue) {
                    result["SecondsAvgPerType"] = Math.Round(log.MsAvgPerType.Value / 1000.0, 3);
                }

                return result;
            }).ToList()
        };

        this.storage.Output().Json<object>().Write(logData);
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