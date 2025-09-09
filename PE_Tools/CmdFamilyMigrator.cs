using Json.Schema.Generation;
using PeLib;
using PeRevitUI;
using PeServices.Aps;
using PeServices.Aps.Models;
using PeServices.Storage;
using PeUtils.Files;
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
public class CmdFamilyMigrator : IExternalCommand {
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
        var parameters = new[] {
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
        var allParameterData = new Dictionary<string, FamilyParameterInfo>();

        try {
            var storage = new Storage("FamilyMigrator");
            var settings = storage.Settings().Json<FamilyMigratorSettings>().Read();
            var paramSvcIds = GetParamSvcParamIds(storage, settings);

            foreach (var family in families) {
                _ = balloon.Add(Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");
                var (fam, famErr) = Families.EditAndLoad(doc, family,
                    famDoc => AddFamilyParams(famDoc, parameters, settings.OverrideExistingValues),
                    famDoc => AddParamSvcParams(famDoc, paramSvcIds),
                    famDoc => SortParams(famDoc, ParametersOrder.Ascending)
                );
                if (famErr is not null) {
                    _ = balloon.Add(Log.ERR, famErr.Message);
                    return Result.Failed;
                }

                foreach (var p in parameters) allParameterData[p.Name] = p;
            }

            // Save all parameter data to CSV at once
            var csv = storage.Output().Csv<FamilyParameterInfo>();
            csv.Write(allParameterData);
            if (settings.OpenOutputFilesOnCommandFinish)
                FileUtils.OpenInDefaultApp(csv.FilePath);

            balloon.Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            _ = TaskDialog.Show("Error",
                $"{ex.Message}  \n {ex.StackTrace} \n {ex.InnerException?.Message} \n {ex.InnerException?.StackTrace}");
            return Result.Cancelled;
        }
    }

    private static ParametersApi.Parameters GetParamSvcParamIds(Storage storage, FamilyMigratorSettings settings) {
        var parameters = new ParametersApi.Parameters();
        var cache = storage.State().Json<ParamSvcCache>("parameters-service-cache.json").Read();
        if (cache.LastRead > DateTime.Now.AddMinutes(-10)) parameters = GetParamServiceParams(settings);
        else
            parameters.Results = cache.ParameterIds
                .Select(id => new ParametersApi.Parameters.ParametersResult { Id = id }).ToList();
        return parameters;
    }

    private static void AddFamilyParams(
        Document famDoc,
        FamilyParameterInfo[] parameters,
        bool overrideExistingValue
    ) {
        var fm = famDoc.FamilyManager;

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

                    p.AddParamResult = fm.get_Parameter(p.Name) != p.Value
                        ? new Exception($"Parameter {p.Name} was not set to {p.Value}")
                        : param;
                } catch (Exception ex) {
                    p.AddParamResult = ex;
                }
            }
        }
    }

    private static void AddParamSvcParams(
        Document famDoc,
        // FamilyMigratorSettings settings,
        ParametersApi.Parameters paramSvcIds
    ) {
        var sharedParameterElements = GetParamSvcParams(famDoc, paramSvcIds);
        foreach (var sharedParam in sharedParameterElements) {
            // SharedParameterElement contains the ExternalDefinition we need
            // We can get it directly from the element, not through GetDefinition()
            try {
                // Add the shared parameter to the family using the shared parameter element
                // TODO: uncomment after first phase of tests
                // var externalDefinition = doc.Application.OpenSharedParameterFile()?.Groups?
                //     .SelectMany(g => g.Definitions)
                //     .OfType<ExternalDefinition>()
                //     .FirstOrDefault(def => def.GUID == sharedParam.GuidValue);
                //
                // if (externalDefinition != null) _ = fm.AddParameter(externalDefinition, GroupTypeId.General, true);
            } catch (Exception ex) {
                throw new Exception($"Failed to add shared parameter {sharedParam.Name}: {ex.Message}");
            }
        }
    }

    private static SharedParameterElement[] GetParamSvcParams(
        Document famDoc,
        // FamilyMigratorSettings settings,
        ParametersApi.Parameters parameters
    ) {
        var balloon = new Balloon();
        if (parameters?.Results == null) _ = balloon.AddDebug(new StackFrame(), Log.ERR, "NO PARAMETERS FOUND");

        var sharedParameterElements = new List<SharedParameterElement>();

        foreach (var p in parameters.Results) {
            if (!p.Name.Contains("Manufacturer")) continue;
            // Create ForgeTypeId from parameter ID
            var parameterTypeId = new ForgeTypeId(p.Id);

            // Download parameter options from APS
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

                var sharedParameterElement = ParameterUtils.DownloadParameter(famDoc, downloadOptions, parameterTypeId)
                                             ?? throw new Exception(
                                                 $"Failed to download parameter: {p.Name} (ID: {p.Id})");

                sharedParameterElements.Add(sharedParameterElement);
            } catch (Exception ex) {
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

        balloon.Show();
        return sharedParameterElements.ToArray();
    }


    /// <summary>
    ///     Gets parameters from the Parameters Service as the types that we've defined.
    ///     This code is taken from the ParametersServiceTest command. and works as expected
    /// </summary>
    private static ParametersApi.Parameters GetParamServiceParams(FamilyMigratorSettings settings) {
        var (acc, gp, col) = (settings.AccountId(), settings.GroupId(), settings.CollectionId());
        var aps = new Aps(settings);

        var messages = new List<string> { "Parameters Service Test", "\n" }; // TODO: delete after testing
        var tcsMessages = new TaskCompletionSource<Result<List<string>>>();

        var tcsParams = new TaskCompletionSource<Result<ParametersApi.Parameters>>();

        _ = Task.Run(async () => {
            try {
                var parameters =
                    await aps.Parameters().GetParameters(acc, gp, col);
                tcsParams.SetResult(parameters);

                tcsMessages.SetResult(messages);
            } catch (Exception ex) {
                tcsMessages.SetResult(ex);
                tcsParams.SetResult(ex);
            }
        });

        tcsMessages.Task.Wait();
        tcsParams.Task.Wait();

        var (msg, msgErr) = tcsMessages.Task.Result;
        if (msgErr != null) throw msgErr;
        foreach (var m in msg) Debug.WriteLine(m);

        var (parameters, paramsErr) = tcsParams.Task.Result;
        return paramsErr != null ? throw paramsErr : parameters;
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

    /// <summary> The result of the parameter creation. The created parameter if successful, or the exception if not. </summary>
    public Result<FamilyParameter> AddParamResult { get; set; }
}

public class ParamSvcCache {
    public DateTime LastRead { get; set; } = DateTime.Now;
    public List<string> ParameterIds { get; set; } = [];
}

public class FamilyMigratorSettings : Storage.BaseSettings, Aps.ITokenProvider {
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

    public string AccountId() => Storage.GlobalSettings().Json().Read().Bim360AccountId;
    public string GroupId() => Storage.GlobalSettings().Json().Read().ParamServiceGroupId;
    public string CollectionId() => Storage.GlobalSettings().Json().Read().ParamServiceCollectionId;
}