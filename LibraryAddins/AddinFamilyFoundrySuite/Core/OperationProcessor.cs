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
    public List<(string Name, Exception Error)> ProcessQueue(Document doc, OperationQueue<TProfile> enqueuer) {
        var results = new List<(string Name, Exception Error)>();
        var familyActions = enqueuer.ToFamilyActions();

        if (doc.IsFamilyDocument) {
            try {
                var saveLocation = this.GetSaveLocations(doc, this.settings);
                _ = doc
                    .ProcessFamily(familyActions)
                    .SaveFamily(saveLocation);
                results.Add((doc.Title, null));
            } catch (Exception ex) {
                results.Add((doc.OwnerFamily.Name, ex));
            }
        } else {
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(this.profile.FilterFamilies.Filter)
                .ToList();

            foreach (var family in families) {
                try {
                    var saveLocation = this.GetSaveLocations(doc, this.settings);
                    _ = doc
                        .EditFamily(family)
                        .ProcessFamily(familyActions)
                        .SaveFamily(saveLocation)
                        .LoadAndCloseFamily(doc, new EditAndLoadFamilyOptions());
                    results.Add((family.Name, null));
                } catch (Exception ex) {
                    results.Add((family.Name, ex));
                }
            }
        }

        return results;
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