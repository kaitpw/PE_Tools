using AddinFamilyFoundrySuite.Core.Settings;
using PeRevit.Families;
using PeRevit.Ui;
using PeServices.Aps.Models;
using PeServices.Storage;
using PeServices.Storage.Core;

namespace AddinFamilyFoundrySuite.Core;

[Transaction(TransactionMode.Manual)]
public abstract class FamilyFoundryBase<TSettings, TProfile> : IFamilyFoundry<TSettings, TProfile>
    where TSettings : BaseSettings<TProfile>, new()
    where TProfile : BaseProfileSettings, new() {
    protected ParametersApi.Parameters _apsParams;
    protected JsonReadWriter<ParametersApi.Parameters> _apsParamsCache;

    public Storage storage { get; private set; }
    public TSettings _settings { get; private set; }
    public TProfile _profile { get; private set; }

    public void InitAndFetch() {
        var storageName = "FamilyFoundry";

        this.storage = new Storage(storageName);
        this._settings = this.storage.Settings().Json<TSettings>().Read();
        this._profile = this._settings.GetProfile();

        // test if the cache exists, if not throw error to prompt user to run command to generate cache
        var tmpParams = this._settings.GetAPSParams();
        if (tmpParams.Results == null) {
            throw new InvalidOperationException(
                $"This Family Foundry command requires cached parameters data, but no cached data exists. " +
                $"Run the \"Cache Parameters Service\" command on a Revit version above 2024 to generate the cache.");
        }

        this._apsParams = tmpParams;
    }

    /// <summary>
    ///     Creates a new fluent processor for building family operations
    /// </summary>
    /// <param name="doc">The document to process</param>
    /// <returns>A processor that can be chained with DocProcess/TypeProcess calls</returns>
    protected OperationEnqueuer EnqueueOperations(Document doc) =>
        new(doc);

    /// <summary>
    ///     Execute a configured processor with full initialization and document handling
    /// </summary>
    protected void ProcessQueue(OperationEnqueuer enqueuer) {
        this.InitAndFetch();
        var balloon = new Balloon();
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
                _ = balloon.Add(Log.TEST, $"Processed family: {family.Name} (ID: {family.Id})");
                // _ = FamUtils.EditAndLoad(doc, family, familyActions);  // Future: could be EditAndLoadAndSave, etc.
                var savePath = this.storage.Output().GetFolderPath();
                _ = FamUtils.EditAndLoadAndSave(doc, family, savePath,
                    familyActions); // Future: could be EditAndLoadAndSave, etc.
            }
        }

        balloon.Show();
    }
}