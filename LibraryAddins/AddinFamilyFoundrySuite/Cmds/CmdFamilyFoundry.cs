using AddinFamilyFoundrySuite.Core;
using PeRevit.Families;
using PeRevit.Ui;
using PeServices.Aps;
using PeServices.Aps.Core;
using PeServices.Aps.Models;
using PeServices.Storage;
using PeServices.Storage.Core;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;
#if !REVIT2023 && !REVIT2024
#endif

namespace AddinFamilyFoundrySuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundry : IExternalCommand {
    private ParametersApi.Parameters _apsParams;
    private Aps _svcAps;
    private Parameters _svcApsParams;
    private JsonReadWriter<ParametersApi.Parameters> _svcStorageCache;
    private FamilyFoundrySettings _settings;

    private List<ParamRemap> _remapData;

    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiapp = commandData.Application;
        var uidoc = uiapp.ActiveUIDocument;
        var doc = uidoc.Document;
        var balloon = new Balloon();

        try {
            this.InitAndFetch();

            var families = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.Name.Contains("Mitsubishi_SUZ-KA09NAHZ"))
                .ToList();

            List<Result<SharedParameterElement>> paramAddResults = [];
            List<Result<FamilyParameter>> paramRemapResults = [];

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, $"Processed family: {family.Name} (ID: {family.Id})");
                var fam = FamUtils.EditAndLoad(doc, family,
                    famDoc => paramAddResults = this.AddParameters(famDoc, this._apsParams),
                    famDoc => paramRemapResults = this.RemapParameters(famDoc, this._remapData));
            }


            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), Log.ERR,
                    $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}")
                .Show();
            return Result.Cancelled;
        }
    }

    private void InitAndFetch() {
        var storageName = "FamilyFoundry";
        var cacheFilename = "parameters-service-cache.json";

        var storage = new Storage(storageName);
        this._svcStorageCache = storage.State().Json<ParametersApi.Parameters>(cacheFilename);
        this._settings = storage.Settings().Json<FamilyFoundrySettings>().Read();
        this._remapData = storage.Settings().Json<List<ParamRemap>>(this._settings.RemapDataFilename).Read();

        this._svcAps = new Aps(this._settings);
        this._svcApsParams = this._svcAps.Parameters(this._settings);
        this._apsParams = Task.Run(async () =>
            await this._svcApsParams.GetParameters(this._svcStorageCache)).Result;
    }

    private List<Result<SharedParameterElement>> AddParameters(Document famDoc, ParametersApi.Parameters psParamInfos) {
        List<Result<SharedParameterElement>> results = [];

        static bool filter(ParamModelRes p) {
            return new[] { "PE_M", "PE_G", "PE_E" }.Any(p.Name.StartsWith);
        }

        results = AddParams.ParamService(famDoc, psParamInfos, filter);

        return results;
    }


    public List<Result<FamilyParameter>> RemapParameters(Document famDoc, List<ParamRemap> paramRemaps) {
        List<Result<FamilyParameter>> results = new();

        var famParams = this.GetFamilyParameters(famDoc);
        foreach (var paramRemap in paramRemaps) {
            try {
                var oldParam = this.ValidateOldParam(famParams, paramRemap.CurrNameOrId);
                var newParam = famParams.First(p => p.Definition.Name == paramRemap.NewNameOrId);
                results.Add(MutateParam.Remap(famDoc, oldParam, newParam));
            } catch (Exception e) {
                results.Add(e);
            }
        }

        return results;
    }

    public FamilyParameter ValidateOldParam(List<FamilyParameter> famParams, string param) {
        var match = famParams.Where(p => p.Definition.Name == param);
        if (!match.Any())
            throw new Exception("Parameter does not exist on this family");
        if (match.Count() > 1)
            throw new Exception("More that one parameter with this name exist on the family.");

        return match.First();
    }

    // get all family parameters
    public List<FamilyParameter> GetFamilyParameters(Document famDoc) {
        var fm = famDoc.FamilyManager;
        var parameterList = new List<FamilyParameter>();
        foreach (FamilyParameter param in fm.Parameters) parameterList.Add(param);
        return parameterList;
    }
}

public class FamilyFoundrySettings : FamilyFoundryBaseSettings {
    public string RemapDataFilename { get; set; } = "remap-data.json";
}

public record ParamRemap {
    public string CurrNameOrId { get; set; }
    public string NewNameOrId { get; set; }
}