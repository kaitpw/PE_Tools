using AddinFamilyFoundrySuite.Core.Settings;
using PeRevit.Families;
using PeRevit.Ui;
using PeServices.Aps.Models;
using PeServices.Storage;
using PeServices.Storage.Core;

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

        if (doc.IsFamilyDocument)
            _ = FamUtils.EditOpenFamily(doc, familyActions);
        else {
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(this._profile.FilterFamilies.Filter)
                .ToList();

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, null, $"Processed family: {family.Name} (ID: {family.Id})");
                // _ = FamUtils.EditAndLoad(doc, family, familyActions);  // Future: could be EditAndLoadAndSave, etc.
                var savePath = this.storage.Output().GetFolderPath();
                _ = FamUtils.EditAndLoadAndSave(doc, family, savePath,
                    familyActions); // Future: could be EditAndLoadAndSave, etc.
            }
        }

        balloon.Show();
    }
}