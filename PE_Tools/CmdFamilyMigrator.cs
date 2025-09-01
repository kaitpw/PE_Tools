using Nice3point.Revit.Extensions;
using PE_Tools.Properties;
using PeRevitUI;
using PeServices;

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
        foreach (var family in families) {
            _ = balloon.Add(Balloon.Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");
            var (fam, famErr) = MigrateProjectFamily(doc, family,
                fm => AddParameters(fm, parameters, true), // !!!!!!!!!!!!!!!!!!!!!!!!
                fm => CleanParameters(fm, ParametersOrder.Ascending, false)
            );
            if (famErr is not null) {
                _ = balloon.Add(Balloon.Log.ERR, famErr.Message);
                return Result.Failed;
            }

            // Query parameters based on whether they're instance or type parameters
            foreach (var param in parameters) {
                var foundParam = VerifyNewParameter(doc, fam, param);

                if (foundParam != null) {
                    _ = balloon.AddDebug(Balloon.Log.TEST, new StackFrame(),
                        $"New param post-load ({(param.IsInstance ? "Instance" : "Type")}): {foundParam.Definition.Name}: {foundParam.AsString()}"); // this should now work
                } else {
                    _ = balloon.AddDebug(Balloon.Log.ERR, new StackFrame(),
                        $"Failed to find parameter '{param.Name}' after load");
                }
            }
        }

        balloon.Show();
        return Result.Succeeded;
    }

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "Family Migrator",
            MethodBase.GetCurrentMethod()?.DeclaringType?.FullName,
            Resources.Yellow_32,
            Resources.Yellow_16,
            "Open the family migrator to migrate a project family in-place."
        ).Data;

    private static Result<Family> MigrateProjectFamily(Document doc,
        Family family,
        params Action<FamilyManager>[] callbacks) {
        var famDoc = doc.EditFamily(family);
        if (!famDoc.IsFamilyDocument) return new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            return new InvalidOperationException("Family documents FamilyManager is null.");

        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        foreach (var callback in callbacks) callback(famDoc.FamilyManager);
        _ = transFamily.Commit();


        var fam = famDoc.LoadFamily(doc, new FamilyOption());
        if (fam is null) return new InvalidOperationException("Failed to load family after edit.");
        var closed = famDoc.Close(false);
        if (!closed) return new InvalidOperationException("Failed to close family document after load error.");
        return fam;
    }


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

                    p.Result = param;
                } catch (Exception ex) {
                    p.Result = ex;
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
    /// <param name="fm"></param>
    /// <param name="order"></param>
    /// <param name="deleteEmpty"></param>
    private static void CleanParameters(FamilyManager fm, ParametersOrder order, bool deleteEmpty) {
        fm.SortParameters(order);
        var parametersToDelete = fm.GetParameters() // to do, figure out filters before deleting anything
            .Where(p => deleteEmpty) // prob need to get FamilyManager.Types first to get actual values.
            .Where(p => p.IsReadOnly)
            .ToList();
    }

    private static Parameter VerifyNewParameter(Document doc, Family fam, FamilyParameterInfo param) {
        Parameter foundParam = null;

        if (param.IsInstance) {
            // For instance parameters, we need to find a family instance
            // Get the first family instance of this family
            // make this work without placing a family instance in the document
            var familyInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.Id == fam.Id)
                .ToList();

            if (familyInstances.Any()) foundParam = familyInstances.First().FindParameter(param.Name);
        } else {
            // Type parameters are queried on the family symbols
            var familySymbols = fam.GetFamilySymbolIds();

            if (familySymbols.Any()) {
                // Try all symbols to find the parameter
                foreach (var symbolId in familySymbols) {
                    var symbol = doc.GetElement(symbolId) as FamilySymbol;
                    if (symbol != null) foundParam = symbol.FindParameter(param.Name);
                }
            }
        }

        return foundParam;
    }
}

public record FamilyParameterInfo {
    public string Name { get; init; }
    public ForgeTypeId Group { get; init; } // must find how to default to other
    public ForgeTypeId Category { get; init; }
    public bool IsInstance { get; init; } = true;
    public object Value { get; init; }

    /// <summary> The result of the parameter creation. The created parameter if successful, or the exception if not. </summary>
    public Result<FamilyParameter> Result { get; set; }
}

public record SharedParameterInfo(
    ExternalDefinition familyDefinition,
    ForgeTypeId groupTypeId,
    bool isInstance
);

internal class FamilyOption : IFamilyLoadOptions {
    public bool OnFamilyFound(
        bool familyInUse,
        out bool overwriteParameterValues) {
        overwriteParameterValues = true;
        return true;
    }

    public bool OnSharedFamilyFound(
        Family sharedFamily,
        bool familyInUse,
        out FamilySource source,
        out bool overwriteParameterValues) {
        source = FamilySource.Project;
        overwriteParameterValues = true;
        return true;
    }
}