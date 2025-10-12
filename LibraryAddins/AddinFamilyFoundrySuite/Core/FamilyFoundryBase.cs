using AddinFamilyFoundrySuite.Core.Settings;
using PeRevit.Ui;
using PeServices.Storage;

namespace AddinFamilyFoundrySuite.Core;

[Transaction(TransactionMode.Manual)]
public abstract class FamilyFoundryBase<TSettings, TProfile>
    where TSettings : BaseSettings<TProfile>, new()
    where TProfile : BaseProfileSettings, new() {
    public Storage storage { get; private set; }
    public TSettings _settings { get; private set; }
    public TProfile _profile { get; private set; }

    protected bool IsInitialized { get; private set; }


    public void Init(Action? customInit = null) {
        var storageName = "FamilyFoundry";

        this.storage = new Storage(storageName);
        this._settings = this.storage.Settings().Json<TSettings>().Read();
        this._profile = this._settings.GetProfile();

        customInit?.Invoke();
        this.IsInitialized = true;
    }

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling
    /// </summary>
    protected void ProcessQueue(OperationEnqueuer enqueuer) {
        if (!this.IsInitialized)
            throw new InvalidOperationException("Must call Init() before ProcessQueue()");

        var balloon = new Ballogger();
        var doc = enqueuer.doc;
        var familyActions = enqueuer.ToFamilyActions();

        if (doc.IsFamilyDocument) {
            var saveLocation = this.GetSaveLocations(doc, this._settings);


            _ = doc
                .ProcessFamily(familyActions)
                .SaveFamily(saveLocation);
        } else {
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(this._profile.FilterFamilies.Filter)
                .ToList();

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, null, $"Processing family: {family.Name} (ID: {family.Id})");
                var saveLocation = this.GetSaveLocations(doc, this._settings);
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