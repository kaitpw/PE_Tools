using AddinFamilyFoundrySuite.Core;
using PeRevit.Families;
using PeRevit.Ui;
using PeServices.Aps;
using PeServices.Aps.Core;
using PeServices.Aps.Models;
using PeServices.Storage;
using PeServices.Storage.Core;
using System.ComponentModel.DataAnnotations;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;
#if !REVIT2023 && !REVIT2024
#endif

namespace AddinFamilyFoundrySuite.Cmds;

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundry : IExternalCommand {
    private ParametersApi.Parameters _apsParams;

    private List<ParamRemap> _remapData;
    private FamilyFoundrySettings _settings;
    private Aps _svcAps;
    private Parameters _svcApsParams;
    private JsonReadWriter<ParametersApi.Parameters> _svcStorageCache;

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
                .Where(f =>
                    f.Name.Contains("Mitsubishi_SUZ-KA09NAHZ")
                    || f.Name.Contains("Mitsubishi_MSZ-EF"))
                .ToList();

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, $"Processed family: {family.Name} (ID: {family.Id})");
                var fam = FamUtils.EditAndLoad(doc, family,
                    famDoc => {
                        Debug.WriteLine($"Processing family: {family.Name}");
                        Debug.WriteLine($"Types: {famDoc.FamilyManager.Types.Size}");
                        Debug.WriteLine($"Parameters: {famDoc.FamilyManager.Parameters.Size}");
                    },
                    famDoc => this.AddParameters(famDoc, this._apsParams),
                    famDoc => this.RemapParameters(famDoc, this._remapData));
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

        if (!famDoc.IsFamilyDocument)
            throw new Exception("Family document is null or not a family document");

        var famParams = this.GetFamilyParameters(famDoc);
        var fm = famDoc.FamilyManager;
        var familyTypes = fm.Types.Cast<FamilyType>().ToList(); // Evaluate once
        var paramPairs = new List<(FamilyParameter oldParam, FamilyParameter newParam)>();

        foreach (var paramRemap in paramRemaps) {
            try {
                var oldParam = this.ValidateOldParam(famParams, paramRemap.CurrNameOrId);
                var newParam = famParams.First(p => p.Definition.Name == paramRemap.NewNameOrId);
                paramPairs.Add((oldParam, newParam));
                results.Add(newParam); // Success result
            } catch (Exception e) {
                results.Add(e);
            }
        }

        foreach (var famType in familyTypes) {
            fm.CurrentType = famType;
            foreach (var (oldParam, newParam) in paramPairs) {
                try {
                    var currentValue = oldParam.GetValue(famType);
                    if (currentValue != null) {
                        _ = newParam.SetValueCoerced(fm, currentValue);
                    }
                } catch {
                    // Individual value setting failures shouldn't stop the entire process
                    // The outer try-catch for parameter validation already handled major errors
                }
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
    [Required] public string RemapDataFilename { get; set; } = "remap-data.json";
}

public record ParamRemap {
    public string CurrNameOrId { get; set; }
    public string NewNameOrId { get; set; }
}