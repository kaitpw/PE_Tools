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

            static bool filter(ParamModelRes p) {
                var includeList = new[] { "PE_M", "PE_G", "PE_E" };
                var excludeList = new[] { "PE_M_GRD", "PE_G_Dim_Clearance" };
                return includeList.Any(p.Name.StartsWith)
                       && !excludeList.Any(p.Name.StartsWith);
            }


            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, $"Processed family: {family.Name} (ID: {family.Id})");
                var fam = FamUtils.EditAndLoad(doc, family,
                    famDoc => {
                        Debug.WriteLine($"\nProcessing family: {family.Name}");
                        Debug.WriteLine($"Types: {famDoc.FamilyManager.Types.Size}");
                        Debug.WriteLine($"Parameters: {famDoc.FamilyManager.Parameters.Size}");
                    },
                    famDoc => AddParams.ParamService(famDoc, this._apsParams, filter),
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


    public List<Result<FamilyParameter>> RemapParameters(Document famDoc, List<ParamRemap> paramRemaps) {
        List<Result<FamilyParameter>> results = new();

        if (!famDoc.IsFamilyDocument)
            throw new Exception("Family document is null or not a family document");

        var fm = famDoc.FamilyManager;
        var familyTypes = fm.Types.Cast<FamilyType>().ToList(); // Evaluate once

        var famParams = this.GetFamilyParameters(famDoc);
        var paramPairs = new List<(FamilyParameter oldParam, FamilyParameter newParam, string policy)>();
        foreach (var paramRemap in paramRemaps) {
            try {
                var oldParam = this.ValidateOldParam(famParams, paramRemap.CurrNameOrId);
                var newParam = famParams.First(p => p.Definition.Name == paramRemap.NewNameOrId);
                paramPairs.Add((oldParam, newParam, paramRemap.MappingPolicy));
            } catch { } // TODO: make an informative error message to prompt user to fix settings
        }

        foreach (var famType in familyTypes) {
            fm.CurrentType = famType;
            foreach (var (oldParam, newParam, policy) in paramPairs) {
                try {
                    results.Add(famDoc.MapValue(oldParam, newParam, policy));
                    // for debugging
                    var (newValue, valErr) = results.Last();
                    Debug.WriteLine($"Set {oldParam.Definition.Name} -> {newParam.Definition.Name} (Policy: {policy})");
                    Debug.WriteLine(
                        $"  {oldParam.StorageType}({fm.GetValue(oldParam)}) -> {newParam.StorageType}({fm.GetValue(newValue)})");
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
    public string MappingPolicy { get; set; } = "Strict"; // Default policy
}