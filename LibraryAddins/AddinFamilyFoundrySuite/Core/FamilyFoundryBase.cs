using PeRevit.Families;
using PeRevit.Ui;
using PeServices.Aps;
using PeServices.Aps.Core;
using PeServices.Aps.Models;
using PeServices.Storage;
using PeServices.Storage.Core;
using AddinFamilyFoundrySuite.Core.Settings;
using PeRevit.Utils;

namespace AddinFamilyFoundrySuite.Core;

[Transaction(TransactionMode.Manual)]
public abstract class FamilyFoundryBase<TSettings, TProfile> : IFamilyFoundry<TSettings, TProfile>
    where TSettings : BaseSettings<TProfile>, new()
    where TProfile : BaseProfileSettings, new() {

    public Storage storage { get; private set; }
    public TSettings _settings { get; private set; }
    public TProfile _profile { get; private set; }
    protected JsonReadWriter<ParametersApi.Parameters> _apsParamsCache = null;
    protected ParametersApi.Parameters _apsParams;

    public void FetchAPSParams() {
        var cacheFilename = "parameters-service-cache.json";
        this._apsParamsCache = this.storage.State().Json<ParametersApi.Parameters>(cacheFilename);

        var revitVersion = int.Parse(Utils.GetRevitVersion());
        if (revitVersion <= 2024) {
            try {
                this._apsParams = this._apsParamsCache.Read();
                return;
            } catch (FileNotFoundException) {
                throw new InvalidOperationException(
                    $"Revit {revitVersion} requires cached parameters data, but no cache file exists. " +
                    "Please run this command on Revit 2025+ first to generate the cache.");
            }
        }

        var svcAps = new Aps(this._settings);
        this._apsParams = Task.Run(async () =>
            await svcAps.Parameters(this._settings).GetParameters(
                this._apsParamsCache, this._settings.UseCachedParametersServiceData)
        ).Result;
    }
    public void InitAndFetch() {
        var storageName = "FamilyFoundry";

        this.storage = new Storage(storageName);
        this._settings = this.storage.Settings().Json<TSettings>().Read();
        this._profile = this._settings.GetProfile();
        this.FetchAPSParams();
    }

    /// <summary>
    /// Creates a new fluent processor for building family operations
    /// </summary>
    /// <param name="doc">The document to process</param>
    /// <returns>A processor that can be chained with DocProcess/TypeProcess calls</returns>
    protected OperationEnqueuer EnqueueOperations(Document doc) =>
        new OperationEnqueuer(doc);

    /// <summary>
    /// Execute a configured processor with full initialization and document handling
    /// </summary>
    protected void ProcessQueue(OperationEnqueuer enqueuer) {
        this.InitAndFetch();
        var balloon = new Balloon();
        var doc = enqueuer.doc;
        var familyActions = enqueuer.ToFamilyActions();

        if (doc.IsFamilyDocument) {
            _ = FamUtils.EditOpenFamily(doc, familyActions);
        } else {
            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(this._profile.FilterFamilies.Filter)
                .ToList();

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, $"Processed family: {family.Name} (ID: {family.Id})");
                // _ = FamUtils.EditAndLoad(doc, family, familyActions);  // Future: could be EditAndLoadAndSave, etc.
                var savePath = this.storage.Output().GetFolderPath();
                _ = FamUtils.EditAndLoadAndSave(doc, family, savePath, familyActions);  // Future: could be EditAndLoadAndSave, etc.
            }
        }

        balloon.Show();
    }
}
