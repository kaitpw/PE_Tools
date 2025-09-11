using NJsonSchema;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using PeRevit.Families;
using PeRevit.Ui;
using PeServices.Aps;
using PeServices.Aps.Core;
using PeServices.Aps.Models;
using PeServices.Storage;
#if !REVIT2023 && !REVIT2024
#endif

namespace PE_Tools;

// TODO: 
// - add support for all param types (both in creating and verifying)
// - add support for shared parameters/parameters service
// - add support for formulas
// - add support for getting the value from an existing parameter
// - add support for cleaning family
//     - purge unused nested families
//     - delete unused reference lines
//     - delete unused params with no value (may need to discriminate more specifically)
//     - maybe delete certain reference lines
//     - delete linear dimensions (maybe?)
// - retroactively change the group of the params

[Transaction(TransactionMode.Manual)]
public class CmdFamilyFoundry : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elementSet
    ) {
        var uiapp = commandData.Application;
        var uidoc = uiapp.ActiveUIDocument;
        var doc = uidoc.Document;


        // Get the first editable family in the project
        var families = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.IsEditable)
            .Where(f => f.Name.Contains("Price LBP15A Exhaust")) // Price LBP15A Exhaust, Fantech RC Series, 
            .ToList();

        // // TODO: remove this after testing family parameter additions
        // var famParamInfos = new[] {
        //     new AddParams.FamilyParamInfo {
        //         Name = "TEST5_Instance",
        //         Group = GroupTypeId.General,
        //         Category = SpecTypeId.String.Text,
        //         IsInstance = true,
        //         Value = "TEST1"
        //     },
        //     new AddParams.FamilyParamInfo {
        //         Name = "TEST5_Type",
        //         Group = GroupTypeId.General,
        //         Category = SpecTypeId.String.Text,
        //         IsInstance = false,
        //         Value = "TEST1"
        //     }
        // };


        var balloon = new Balloon();

        try {
            var storage = new Storage("FamilyFoundry");
            var settings = storage.Settings().Json<FamilyFoundrySettings>().Read();
            var svcAps = new Aps(settings);
            var svcApsParams = svcAps.Parameters(settings);
            var psParamInfos = GetParamSvcParamInfo(storage, svcApsParams);
            List<Result<SharedParameterElement>> psParamsDownloadResults = [];
            List<Result<FamilyParameter>> psParamAdditionResults = [];

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");
                var fam = FamUtils.EditAndLoad(doc, family,
                    // (famDoc, results) => {
                    //     var result = AddParams.Family(famDoc, famParamInfos,
                    //         settings.ParameterAdditionSettings.FamilyParameter.OverrideExistingValues);
                    //     results.Add(nameof(AddParams.Family), result);
                    // },
                    famDoc => {
                        var recoverFromErrorSettings = settings.ParameterAdditionSettings.ParametersService
                            .RecoverFromErrorSettings;
                        psParamsDownloadResults = AddParams.ParamService(famDoc,
                            recoverFromErrorSettings, psParamInfos);
                    },
                    // famDoc => {
                    //     var psParams = psParamsDownloadResults
                    //         .Where(p => p.AsTuple().value != null)
                    //         .Select(p => p.AsTuple().value)
                    //         .ToList();
                    //     psParamAdditionResults = AddParams.ParamSvc(famDoc, psParams);
                    // },
                    famDoc => SortParams(famDoc, ParametersOrder.Ascending)
                );
            }

            // tODO: write to output somehow

            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), Log.ERR,
                $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}").Show();
            return Result.Cancelled;
        }
    }

    private static ParametersApi.Parameters GetParamSvcParamInfo(Storage storage, Parameters svcApsParams) {
        const string cacheFileName = "parameters-service-cache.json";
        var cache = storage.State().Json<ParametersApi.Parameters>(cacheFileName);
        var tcsParams = new TaskCompletionSource<Result<ParametersApi.Parameters>>();
        _ = Task.Run(async () => {
            try {
                tcsParams.SetResult(await svcApsParams.GetParameters(cache));
            } catch (Exception ex) {
                tcsParams.SetResult(ex);
            }
        });
        tcsParams.Task.Wait();

        var (parameters, paramsResult) = tcsParams.Task.Result;
        return paramsResult != null ? throw paramsResult : parameters;
    }

    private static void SortParams(Document famDoc, ParametersOrder order) =>
        famDoc.FamilyManager.SortParameters(order);
}

public class FamilyFoundrySettings : Storage.BaseSettings, Aps.IOAuthTokenProvider, Aps.IParametersTokenProvider {
    [Description(
        "Use cached Parameters Service data instead of downloading from APS on every run. " +
        "Only set to true if you are sure no one has changed the param definitions since the last time you opened Revit " +
        "and/or you are running this command in quick succession.")]
    [Required]
    public bool UseCachedParametersServiceData { get; set; } = true;

    [Description("Remove parameters that have no values during family cleanup operations")]
    [Required]
    public bool DeleteEmptyParameters { get; set; } = true; // unused right now

    [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
    [Required]
    public bool OpenOutputFilesOnCommandFinish { get; set; } = true;

    public ParameterAdditionSettings ParameterAdditionSettings { get; set; } = new();

    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;
    public string GetAccountId() => Storage.GlobalSettings().Json().Read().Bim360AccountId;
    public string GetGroupId() => Storage.GlobalSettings().Json().Read().ParamServiceGroupId;
    public string GetCollectionId() => Storage.GlobalSettings().Json().Read().ParamServiceCollectionId;
}