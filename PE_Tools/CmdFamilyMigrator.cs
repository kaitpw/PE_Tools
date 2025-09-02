using Json.Schema.Generation;
using Nice3point.Revit.Extensions;
using PE_Tools.Properties;
using PeLib;
using PeRevitUI;
using PeServices;
using PeUtils.Files;

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

public class FamilyMigratorSettings : SettingsManager<FamilyMigratorSettings>.IBaseSettings {
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. This WILL NOT override existing family instances' values.")]
    [Required]
    public bool OverrideExistingValues { get; set; } = true;

    [Description("Remove parameters that have no values during family cleanup operations")]
    [Required]
    public bool DeleteEmptyParameters { get; set; } = true; // unused right now

    [Description("Automatically open output files (CSV, etc.) when commands complete successfully")]
    [Required]
    public bool OpenOutputFilesOnCommandFinish { get; set; } = true;
}

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
        var storage = new Storage("FamilyMigrator");

        // Get the first editable family in the project
        var families = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.IsEditable)
            .Where(f => f.Name.Contains("Price JS-1_Slot")) // TODO: remove this filter, it's just for testing
            .ToList();

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
            var settings = storage.Settings<FamilyMigratorSettings>().Json().Read();

            foreach (var family in families) {
                _ = balloon.Add(Balloon.Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");
                var (fam, famErr) = Families.EditAndLoad(doc, family,
                    fm => AddParameters(fm, parameters, settings.OverrideExistingValues),
                    fm => CleanParameters(fm, ParametersOrder.Ascending, settings.DeleteEmptyParameters)
                );
                if (famErr is not null) {
                    _ = balloon.Add(Balloon.Log.ERR, famErr.Message);
                    return Result.Failed;
                }

                // Query parameters based on whether they're instance or type parameters
                foreach (var param in parameters) {
                    VerifyNewParameter(doc, fam, param);
                    allParameterData[param.Name] = param;
                }
            }

            // Save all parameter data to CSV at once
            var csv = storage.Output<FamilyParameterInfo>().Csv();
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

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "Family Migrator",
            MethodBase.GetCurrentMethod()?.DeclaringType?.FullName,
            Resources.Yellow_32,
            Resources.Yellow_16,
            "Open the family migrator to migrate a project family in-place."
        ).Data;


    private static void AddParameters(
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

                    p.EditResult = param;
                } catch (Exception ex) {
                    p.EditResult = ex;
                }
            }
        }
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

    private static void VerifyNewParameter(Document doc, Family fam, FamilyParameterInfo param) {
        if (param.IsInstance) {
            // TODO: make this work without placing a family instance in the document
            var familyInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.Id == fam.Id)
                .ToList();

            // TODO: make this check for the default instance value, not the instance value of a particular instance
            if (familyInstances.Any()) param.VerifiedResult = familyInstances.First().FindParameter(param.Name) != null;
        } else {
            var familySymbols = fam.GetFamilySymbolIds();
            if (!familySymbols.Any()) param.VerifiedResult = false;
            foreach (var symbolId in familySymbols) {
                if (doc.GetElement(symbolId) is FamilySymbol symbol)
                    param.VerifiedResult = symbol.FindParameter(param.Name)?.AsString() == param.Value.ToString();
            }
        }
    }
}

public record FamilyParameterInfo {
    public string Name { get; init; }
    public ForgeTypeId Group { get; init; } // must find how to default to other
    public ForgeTypeId Category { get; init; }
    public bool IsInstance { get; init; } = true;
    public object Value { get; init; }

    /// <summary> The result of the parameter creation. The created parameter if successful, or the exception if not. </summary>
    public Result<FamilyParameter> EditResult { get; set; }

    public bool VerifiedResult { get; set; }
}

public record SharedParameterInfo(
    ExternalDefinition familyDefinition,
    ForgeTypeId groupTypeId,
    bool isInstance
);