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
//     - delete unused params
//     - delete parameters with no value
//     - maybe delete certain reference lines

// public record ParamAdditionResults {
//     public string Name
// }

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
            foreach (var family in families) {
                _ = balloon.Add(Balloon.Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");
                var (fam, famErr) = Families.EditAndLoad(doc, family,
                    fm => AddFamilyParameters(fm, parameters, settings.OverrideExistingValues),
                    fm => CleanParameters(fm, ParametersOrder.Ascending, settings.DeleteEmptyParameters),
                    fm => AddSharedParameters(doc, fm, settings)
                );
                if (famErr is not null) {
                    _ = balloon.Add(Balloon.Log.ERR, famErr.Message);
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
            _ = TaskDialog.Show("Error", ex.Message);
            return Result.Cancelled;
        }
    }

    private static void AddFamilyParameters(
        FamilyManager fm,
        FamilyParameterInfo[] parameters,
        bool overrideExistingValue
    ) {
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

    private static void AddSharedParameters(
        Document doc,
        FamilyManager fm,
        FamilyMigratorSettings settings
    ) {
        var sharedParameterElements = AddParameterServiceParameters(doc, settings);
        foreach (var sharedParam in sharedParameterElements) {
            // SharedParameterElement contains the ExternalDefinition we need
            // We can get it directly from the element, not through GetDefinition()
            try {
                // Add the shared parameter to the family using the shared parameter element
                var externalDefinition = doc.Application.OpenSharedParameterFile()?.Groups?
                    .SelectMany(g => g.Definitions)
                    .OfType<ExternalDefinition>()
                    .FirstOrDefault(def => def.GUID == sharedParam.GuidValue);

                if (externalDefinition != null) _ = fm.AddParameter(externalDefinition, GroupTypeId.General, true);
            } catch (Exception ex) {
                throw new Exception($"Failed to add shared parameter {sharedParam.Name}: {ex.Message}");
            }
        }
    }

    private static SharedParameterElement[] AddParameterServiceParameters(
        Document doc,
        FamilyMigratorSettings settings
    ) {
        var parameters = GetParamServiceParams(settings);
        if (parameters?.Results == null) throw new Exception("NO PARAMETERS FOUND");

        var sharedParameterElements = new List<SharedParameterElement>();

        foreach (var p in parameters.Results) {
            // Create ForgeTypeId from parameter ID
            var parameterTypeId = new ForgeTypeId(p.Id);

            // Download parameter options from APS
            var downloadOptions = ParameterUtils.DownloadParameterOptions(parameterTypeId);
            downloadOptions.SetCategories(p.Metadata.CategorySet(doc));
            downloadOptions.IsInstance = p.Metadata.IsInstance;
            downloadOptions.Visible = !p.Metadata.Visible;
            downloadOptions.SetGroupTypeId(p.Metadata.GroupTypeId);
            var sharedParameterElement = ParameterUtils.DownloadParameter(doc, downloadOptions, parameterTypeId)
                                         ?? throw new Exception($"Failed to download parameter: {p.Name} (ID: {p.Id})");

            sharedParameterElements.Add(sharedParameterElement);
        }

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
                var hubs = await aps.Hubs().GetHubs();
                var hub = hubs.Data.First().Id;
                if (!string.IsNullOrEmpty(acc)) hub = acc;

                var groups = await aps.Parameters().GetGroups(hub);
                var group = groups.Results.First().Id;
                if (!string.IsNullOrEmpty(gp)) group = gp;

                var collections = await aps.Parameters().GetCollections(hub, group);
                var collection = collections.Results.First().Id;
                if (!string.IsNullOrEmpty(col)) collection = col;

                var parameters =
                    await aps.Parameters().GetParameters(hub, group, collection);
                tcsParams.SetResult(parameters);

                messages.AddRange(hubs.Data.Select(h => "Hubs (plural)      : " + h.Id));
                messages.Add("SELECTED          -> " + hub + "\n");
                messages.AddRange(groups.Results.Select(g => "Groups (plural)    : " + g.Id));
                messages.Add("SELECTED          -> " + group + "\n");
                messages.AddRange(collections.Results.Select(c => "Collections (plural): " + c.Title + ": " + c.Id));
                messages.Add("SELECTED          -> " + collection + "\n");
                messages.AddRange(parameters.Results.Select(p => "Parameters (plural): " + p.Name));

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

    /// <summary>
    ///     TODO:
    ///     - purge unused nested families
    ///     - delete unused params with no value (may need to discriminate more specifically)
    ///     - delete unused reference lines
    ///     - delete linear dimensions (maybe?)
    /// </summary>
    private static void CleanParameters(FamilyManager fm, ParametersOrder order, bool deleteEmpty) {
        fm.SortParameters(order);
        var parametersToDelete = fm.GetParameters() // to do, figure out filters before deleting anything
            .Where(p => deleteEmpty) // prob need to get FamilyManager.Types first to get actual values.
            .Where(p => p.IsReadOnly)
            .ToList();
    }
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

public class FamilyMigratorSettings : Storage.BaseSettings, Aps.ITokenProvider {
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