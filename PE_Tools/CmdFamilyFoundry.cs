using Json.Schema.Generation;
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
//

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
            .Where(f => f.Name.Contains("Price JS-1_Slot")) // TODO: remove this filter, it's just for testing
            .ToList();

        // TODO: remove this after testing family parameter additions
        var famParamInfos = new[] {
            new AddParams.FamilyParamInfo {
                Name = "TEST5_Instance",
                Group = GroupTypeId.General,
                Category = SpecTypeId.String.Text,
                IsInstance = true,
                Value = "TEST1"
            },
            new AddParams.FamilyParamInfo {
                Name = "TEST5_Type",
                Group = GroupTypeId.General,
                Category = SpecTypeId.String.Text,
                IsInstance = false,
                Value = "TEST1"
            }
        };


        var balloon = new Balloon();

        try {
            var storage = new Storage("FamilyFoundry");
            var settings = storage.Settings().Json<FamilyFoundrySettings>().Read();
            var aps = new Aps(settings);
            var apsParams = aps.Parameters(settings);
            var paramSvcIds = GetParamSvcParamIds(storage, apsParams);

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");
                var (fam, operationResults) = FamUtils.EditAndLoad(doc, family,
                    (famDoc, results) => {
                        var result = AddParams.Family(famDoc, famParamInfos, settings.OverrideExistingValues);
                        results.Add(nameof(AddParams.Family), result);
                    },
                    (famDoc, results) => {
                        var result = AddParams.ParamSvc(famDoc, paramSvcIds);
                        results.Add(nameof(AddParams.ParamSvc), result);
                    },
                    (famDoc, _) => SortParams(famDoc, ParametersOrder.Ascending)
                );
            }

            // Save all parameter data to CSV at once
            // var csv = storage.Output().Csv<FamilyParameterInfo>();
            // csv.Write(allParameterData);
            // if (settings.OpenOutputFilesOnCommandFinish)
            //     FileUtils.OpenInDefaultApp(csv.FilePath);

            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            _ = TaskDialog.Show("Error",
                $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}");
            return Result.Cancelled;
        }
    }

    private static ParametersApi.Parameters GetParamSvcParamIds(Storage storage, Parameters ApsParameters) {
        const string cacheFileName = "parameters-service-cache.json";
        var cache = storage.State().Json<ParametersApi.Parameters>(cacheFileName);
        var tcsParams = new TaskCompletionSource<Result<ParametersApi.Parameters>>();
        _ = Task.Run(async () => {
            try {
                tcsParams.SetResult(await ApsParameters.GetParameters(cache));
            } catch (Exception ex) {
                tcsParams.SetResult(ex);
            }
        });
        tcsParams.Task.Wait();

        var (parameters, paramsResult) = tcsParams.Task.Result;
        return paramsResult != null ? throw paramsResult : parameters;
    }

    private static SharedParameterElement[] DownloadParamSvcParams(Document famDoc, Parameters apsParams) {
        var downloadParamsResults = apsParams.DownloadParameters(famDoc, paramSvcIds);

        foreach (var result in downloadParamsResults) {
            var (sharedParam, downloadErr) = result;
            if (downloadErr is not null) throw downloadErr;
            // } catch (Exception ex) {
            //     // TODO: FIGURE THIS OUT
            //     var balloon = new Balloon();
            //     var msgBase = $"Error for Parameter {p.Name}: {p.Id}.";
            //     if (ex.IsExceptionFromMethod(nameof(ParameterUtils.DownloadParameterOptions))) {
            //         switch (ex.Message) {
            //         case { } msg when msg.Contains("Object reference not set to an instance of an object."):
            //             _ = balloon.AddDebug(new StackFrame(), Log.ERR, msgBase +
            //                                                             "\nA crucial value of this parameter in Parameters Service is not set, probably the instace/type association");
            //             break;
            //         case { } msg when msg.Contains("Parameter with a matching name"):
            //             continue; // TODO: delete the current param, retry adding new one. need to figure out how to test for an unused param first though
            //         case { } msg when msg.Contains("Parameter with a matching GUID"):
            //             continue; // TODO: Ignore this case? maybe add a log or write to storage output
            //         default:
            //             _ = balloon.AddDebug(new StackFrame(), Log.ERR,
            //                 $"Unknown {msgBase}" +
            //                 $"\nError: {ex.Message}\n{ex.StackTrace}");
            //             break;
            //         }
            //     } else
            //         _ = balloon.AddDebug(new StackFrame(), Log.ERR, msgBase);
            // }
        }


        private static void SortParams(Document famDoc, ParametersOrder order) {
            famDoc.FamilyManager.SortParameters(order);
        }
    }


    public class FamilyFoundrySettings : Storage.BaseSettings, Aps.IOAuthTokenProvider, Aps.IParametersTokenProvider {
        [Description(
            "Use cached Parameters Service data instead of downloading from APS on every run. " +
            "Only set to true if you are sure no one has changed the param definitions since the last time you opened Revit " +
            "and/or you are running this command in quick succession.")]
        [Required]
        public bool UseCachedParametersServiceData { get; set; } = false;

        [Description(
            "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
        [Required]
        public bool OverrideExistingValues { get; set; } = true;

        [Description("Remove parameters that have no values during family cleanup operations")]
        [Required]
        public bool DeleteEmptyParameters { get; set; } = true; // unused right now

        [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
        [Required]
        public bool OpenOutputFilesOnCommandFinish { get; set; } = true;

        public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsDesktopClientId1;
        public string GetClientSecret() => null;
        public string GetAccountId() => Storage.GlobalSettings().Json().Read().Bim360AccountId;
        public string GetGroupId() => Storage.GlobalSettings().Json().Read().ParamServiceGroupId;
        public string GetCollectionId() => Storage.GlobalSettings().Json().Read().ParamServiceCollectionId;
    }