using Nice3point.Revit.Extensions;
using PE_Tools.Properties;
using PeRevitUI;

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
public record FamilyParameterInfo(
    string ParameterName,
    ForgeTypeId GroupTypeId,
    ForgeTypeId FamilyCategory,
    bool IsInstance,
    object value = null // TODO: optional value of any type, keep it as object or is there something better?
);

public record SharedParameterInfo(
    ExternalDefinition familyDefinition,
    ForgeTypeId groupTypeId,
    bool isInstance
);


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

        // Get the first editable family
        var family = families.First();
        var balloon = new Balloon();
        var fam = MigrateProjectFamily(doc, family, balloon);
        balloon.Show();

        return Result.Succeeded;
    }

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "Command Palette",
            MethodBase.GetCurrentMethod()?.DeclaringType?.FullName,
            Resources.Yellow_32,
            Resources.Yellow_16,
            "Open the command palette to search and execute Revit commands quickly. Use Ctrl+K for quick access."
        ).Data;

    private static Family MigrateProjectFamily(Document doc, Family family, Balloon balloon) {
        _ = balloon.Add(Balloon.Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");

        var parameters = new[] {
                new FamilyParameterInfo(
                    "TEST5_Instance",
                    GroupTypeId.General,
                    SpecTypeId.Number,
                    true, // Instance parameter
                    42.0 // Number value instead of string
                ),
                new FamilyParameterInfo(
                    "TEST5_Type",
                    GroupTypeId.General,
                    SpecTypeId.Number,
                    false, // Type parameter
                    42.0 // Number value instead of string
                )
            }
            .Where(p => family.FindParameter(p.ParameterName) == null)
            // .Where(p => check the value's datatype against the spectypeid)
            .ToArray();

        // Open family for editing (no transaction needed)
        var famDoc = doc.EditFamily(family);
        if (!famDoc.IsFamilyDocument) throw new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null)
            throw new InvalidOperationException("Family documents FamilyManager is null.");

        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        var (famEdit, famEditErr) = EditFamily(famDoc.FamilyManager, parameters, balloon);
        if (famEditErr is not null) {
            _ = transFamily.RollBack();
            var closed = famDoc.Close(false);
            if (!closed) _ = balloon.Add(Balloon.Log.ERR, "Failed to close family document after edit error.");
        }

        _ = transFamily.Commit(); // gotta see if this transaction hangline works

        foreach (var param in parameters) {
            _ = balloon.AddDebug(Balloon.Log.TEST, new StackFrame(), 
                $"New param pre-load ({param.IsInstance}): {famDoc.FamilyManager.get_Parameter(param.ParameterName).Definition.Name}"); // this works
        }
        var fam = famDoc.LoadFamily(doc, new FamilyOption());
        if (fam is not null) {
            _ = balloon.AddDebug(Balloon.Log.INFO, new StackFrame(), 
                $"Family loaded successfully: {fam.Name} (ID: {fam.Id})");
            

            
            // Query parameters based on whether they're instance or type parameters
            foreach (var param in parameters) {
                var foundParam = VerifyNewParameter(doc, fam, param);
                
                if (foundParam != null) {
                    _ = balloon.AddDebug(Balloon.Log.TEST, new StackFrame(), 
                        $"New param post-load ({(param.IsInstance ? "Instance" : "Type")}): {foundParam.Definition.Name}: {foundParam.AsInteger()}"); // this should now work
                } else {
                    _ = balloon.AddDebug(Balloon.Log.ERR, new StackFrame(), 
                        $"Failed to find parameter '{param.ParameterName}' after load");
                }
            }
            
            var closed = famDoc.Close(false);
            if (!closed) _ = balloon.Add(Balloon.Log.ERR, "Failed to close family document after load error.");
        }

        return fam;
    }

    private static Result<bool> EditFamily(
        FamilyManager fm,
        FamilyParameterInfo[] parameters,
        Balloon balloon = null
    ) {
        try {
            var paramResults = AddParameters(fm, parameters);
            foreach (var resultKey in paramResults.Keys) {
                var (param, err) = paramResults[resultKey];
                _ = err is null
                    ? balloon?.AddDebug(Balloon.Log.INFO, new StackFrame(),
                        $"Parameter '{param.Definition.Name}' added successfully.")
                    : balloon?.AddDebug(Balloon.Log.ERR, new StackFrame(),
                        $"Failed to add parameter '{resultKey}': {err.Message}");
            }

            if (paramResults.Values.Count() <= 0)
                return false; // TODO: handle this path better?  what to do about empty parameters arrays?
            return true;
        } catch (Exception ex) {
            return ex;
        }
    }

    private static Dictionary<string, Result<FamilyParameter>> AddParameters(
        FamilyManager fm,
        FamilyParameterInfo[] parameters
    ) {
        var resultAggregated = new Dictionary<string, Result<FamilyParameter>>();
        foreach (var p in parameters) {
            try {
                var param = fm.AddParameter(
                    p.ParameterName,
                    p.GroupTypeId,
                    p.FamilyCategory,
                    p.IsInstance
                );
                
                // Set parameter value based on its type, look into storageType of params and spectypeid
                if (p.value != null) {
                    try {
                        if (p.value is double doubleValue) {
                            fm.Set(param, doubleValue);
                        } else if (p.value is int intValue) {
                            fm.Set(param, intValue);
                        } else if (p.value is string stringValue) {
                            fm.SetValueString(param, stringValue);
                        } else {
                            // Try to convert to string as fallback
                            fm.SetValueString(param, p.value.ToString());
                        }
                    } catch (Exception valueEx) {
                        // Log the value setting error but don't fail the parameter creation
                        resultAggregated[p.ParameterName] = new Exception($"Parameter created but value setting failed: {valueEx.Message}");
                        continue;
                    }
                }
                
                resultAggregated[p.ParameterName] = param;
            } catch (Exception ex) {
                resultAggregated[p.ParameterName] = ex;
            }
        }

        return resultAggregated;
    }

    private static Parameter VerifyNewParameter(Document doc, Family fam, FamilyParameterInfo param)
    {
        Parameter foundParam = null;
        
        if (param.IsInstance) {
            // For instance parameters, we need to find a family instance
            // Get the first family instance of this family
            var familyInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.Id == fam.Id)
                .ToList();
            
            if (familyInstances.Any()) {
                foundParam = familyInstances.First().FindParameter(param.ParameterName);
            }
        } else {
            // Type parameters are queried on the family symbols
            var familySymbols = fam.GetFamilySymbolIds();
            
            if (familySymbols.Any()) {
                // Try all symbols to find the parameter
                foreach (var symbolId in familySymbols) {
                    var symbol = doc.GetElement(symbolId) as FamilySymbol;
                    if (symbol != null) {
                        foundParam = symbol.FindParameter(param.ParameterName);
                    }
                }
            }
        }
        
        return foundParam;
    }
}

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