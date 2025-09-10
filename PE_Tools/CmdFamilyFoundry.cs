using Json.Schema.Generation;
using PeLib;
using PeRevitUI;
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
            new FamilyParameterInfo {
                Name = "TEST5_Instance",
                Group = GroupTypeId.General,
                Category = SpecTypeId.String.Text,
                IsInstance = true,
                Value = "TEST1"
            },
            new FamilyParameterInfo {
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
                var (fam, operationResults) = Families.EditAndLoad(doc, family,
                    (famDoc, results) => {
                        var result = AddFamilyParams(famDoc, famParamInfos, settings.OverrideExistingValues);
                        results.Add(nameof(AddFamilyParams), result);
                    },
                    (famDoc, results) => {
                        var result = AddParamSvcParams(famDoc, paramSvcIds);
                        results.Add(nameof(AddParamSvcParams), result);
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
        var cache = storage.State().Json<ParametersApi.Parameters>(cacheFileName).Read();
        var lastWrite = File.GetLastWriteTime(cacheFileName);
        if (lastWrite < DateTime.Now.AddMinutes(-10) && cache.Results.Count > 1) return cache;

        var tcsParams = new TaskCompletionSource<Result<ParametersApi.Parameters>>();

        _ = Task.Run(async () => {
            try {
                tcsParams.SetResult(await ApsParameters.GetParameters());
            } catch (Exception ex) {
                tcsParams.SetResult(ex);
            }
        });

        tcsParams.Task.Wait();
        var (parameters, paramsResult) = tcsParams.Task.Result;
        return paramsResult != null ? throw paramsResult : parameters;
    }

    private static List<Result<FamilyParameter>> AddFamilyParams(
        Document famDoc,
        FamilyParameterInfo[] parameters,
        bool overrideExistingValue
    ) {
        var fm = famDoc.FamilyManager;
        var result = new List<Result<FamilyParameter>>();

        bool NoExistingParam(FamilyParameterInfo p) {
            return fm.get_Parameter(p.Name) == null;
        }

        parameters = parameters
            .Where(p => NoExistingParam(p) || overrideExistingValue)
            .ToArray();
        foreach (FamilyType type in fm.Types) {
            fm.CurrentType = type;
            foreach (var p in parameters) {
                try {
                    var param = fm.get_Parameter(p.Name) ?? fm.AddParameter(p.Name, p.Group, p.Category, p.IsInstance);

                    // Set parameter value based on its type, look into storageType of params and spectypeid
                    if (p.Value != null) {
                        switch (p.Value) {
                        case double doubleValue:
                            fm.Set(param, doubleValue);
                            break;
                        case int intValue:
                            fm.Set(param, intValue);
                            break;
                        case string stringValue:
                            fm.Set(param, stringValue);
                            break;
                        }
                    }

                    result.Add(fm.get_Parameter(p.Name) != p.Value
                        ? new Exception($"Parameter {p.Name} was not set to {p.Value}")
                        : param);
                } catch (Exception ex) {
                    result.Add(ex);
                }
            }
        }

        return result;
    }

    private static List<Result<FamilyParameter>> AddParamSvcParams(
        Document famDoc,
        ParametersApi.Parameters paramSvcIds
    ) {
        var fm = famDoc.FamilyManager;
        var results = new List<Result<FamilyParameter>>();
        var downloadParamsResults = DownloadParamSvcParams(famDoc, paramSvcIds);

        foreach (var result in downloadParamsResults) {
            var (sharedParam, downloadErr) = result;
            if (downloadErr is not null) throw downloadErr;
            // SharedParameterElement contains the ExternalDefinition we need
            // We can get it directly from the element, not through GetDefinition()
            try {
                // Add the shared parameter to the family using the shared parameter element
                // TODO: uncomment after first phase of tests
                var externalDefinition = famDoc.Application.OpenSharedParameterFile()?.Groups?
                    .SelectMany(g => g.Definitions)
                    .OfType<ExternalDefinition>()
                    .FirstOrDefault(def => def.GUID == sharedParam.GuidValue);

                if (externalDefinition != null)
                    results.Add(fm.AddParameter(externalDefinition, GroupTypeId.General, true));
            } catch (Exception ex) {
                throw new Exception($"Failed to add parameter service parameter {sharedParam.Name}: {ex.Message}");
            }
        }

        return results;
    }

    private static Result<SharedParameterElement>[] DownloadParamSvcParams(
        Document famDoc,
        ParametersApi.Parameters parameters
    ) {
        // var balloon = new Balloon();
        var downloadedParams = new List<Result<SharedParameterElement>>();
        if (parameters is { Results: null }) {
            downloadedParams.Add(new Exception("No Parameters Service parameters were found"));
            return downloadedParams.ToArray();
        }

        foreach (var p in parameters.Results) {
            // if (!p.Name.Contains("Manufacturer")) continue;
            var parameterTypeId = new ForgeTypeId(p.Id);

            try {
                var downloadOptions = ParameterUtils.DownloadParameterOptions(parameterTypeId);
                if (downloadOptions.GetCategories().Count == 0) {
                    var owner = famDoc.OwnerFamily;
                    var familyCategory = owner.FamilyCategoryId;
                    if (familyCategory != null) {
                        var familyCategorySet = new HashSet<ElementId> { familyCategory };
                        downloadOptions.SetCategories(familyCategorySet);
                    }
                }

                var sharedParam = ParameterUtils.DownloadParameter(famDoc, downloadOptions, parameterTypeId);
                downloadedParams.Add(sharedParam);
            } catch (Exception ex) {
                // TODO: FIGURE THIS OUT
                var balloon = new Balloon();
                var msgBase = $"Error for Parameter {p.Name}: {p.Id}.";
                if (ex.IsExceptionFromMethod(nameof(ParameterUtils.DownloadParameterOptions))) {
                    switch (ex.Message) {
                    case { } msg when msg.Contains("Object reference not set to an instance of an object."):
                        _ = balloon.AddDebug(new StackFrame(), Log.ERR, msgBase +
                                                                        "\nA crucial value of this parameter in Parameters Service is not set, probably the instace/type association");
                        break;
                    case { } msg when msg.Contains("Parameter with a matching name"):
                        continue; // TODO: delete the current param, retry adding new one. need to figure out how to test for an unused param first though
                    case { } msg when msg.Contains("Parameter with a matching GUID"):
                        continue; // TODO: Ignore this case? maybe add a log or write to storage output
                    default:
                        _ = balloon.AddDebug(new StackFrame(), Log.ERR,
                            $"Unknown {msgBase}" +
                            $"\nError: {ex.Message}\n{ex.StackTrace}");
                        break;
                    }
                } else
                    _ = balloon.AddDebug(new StackFrame(), Log.ERR, msgBase);
            }
        }

        return downloadedParams.ToArray();
    }


    private static void SortParams(Document famDoc, ParametersOrder order) =>
        famDoc.FamilyManager.SortParameters(order);
}

public record FamilyParameterInfo {
    public string Name { get; init; }
    public ForgeTypeId Group { get; init; } // must find how to default to other
    public ForgeTypeId Category { get; init; }
    public bool IsInstance { get; init; } = true;
    public object Value { get; init; }
}

public class ParamSvcCache {
    public DateTime LastRead { get; set; } = DateTime.Now;
    public List<string> ParameterIds { get; set; } = [];
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