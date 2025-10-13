using PeServices.Storage;
using PeRevit.Ui;
namespace AddinFamilyFoundrySuite.Core;

public class OperationProcessor<TProfile>
    where TProfile : BaseProfileSettings, new() {
    public Storage storage { get; private set; }
    public BaseSettings<TProfile> settings { get; private set; }
    public TProfile profile { get; private set; }

    public OperationProcessor(Storage storage) {
        this.storage = storage;
        this.settings = this.storage.Settings().Json<BaseSettings<TProfile>>().Read();
        this.profile = this.settings.GetProfile();
    }

    public OperationQueue<TProfile> CreateQueue() => new OperationQueue<TProfile>(this.profile);

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling
    /// </summary>
    public void ProcessQueue(Document doc, OperationQueue<TProfile> enqueuer) {
        var balloon = new Ballogger();
        var familyActions = enqueuer.ToFamilyActions();

        if (doc.IsFamilyDocument) {
            var saveLocation = this.GetSaveLocations(doc, this.settings);
            _ = doc
                .ProcessFamily(familyActions)
                .SaveFamily(saveLocation);
        } else {
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(this.profile.FilterFamilies.Filter)
                .ToList();

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, null, $"Processing family: {family.Name} (ID: {family.Id})");
                var saveLocation = this.GetSaveLocations(doc, this.settings);
                _ = doc
                    .EditFamily(family)
                    .ProcessFamily(familyActions)
                    .SaveFamily(saveLocation)
                    .LoadAndCloseFamily(doc, new EditAndLoadFamilyOptions());
            }
        }

        balloon.Show();
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