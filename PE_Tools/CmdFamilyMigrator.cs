using Nice3point.Revit.Extensions;
using PE_Tools.Properties;
using PeRevitUI;

namespace PE_Tools;

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

        var param = fam.FindParameter("Test");

        _ = balloon.Add(Balloon.Log.TEST, $"Family result: {fam != null}");
        balloon.Add(Balloon.Log.TEST, $"Param result: {param != null}").Show();

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


    static Dictionary<string, Result<FamilyParameter>> AddParameters(
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
                resultAggregated[p.ParameterName] = param;
            } catch (Exception ex) {
                resultAggregated[p.ParameterName] = ex;
                continue;
            }
        }
        return resultAggregated;
    }


    static Family MigrateProjectFamily(Document doc, Family family, Balloon balloon) {
        _ = balloon.Add(Balloon.Log.TEST, $"Processing family: {family.Name} (ID: {family.Id})");

        var parameters = new FamilyParameterInfo[] {
                new FamilyParameterInfo(
                    "TEST TEST TEST",
                    GroupTypeId.General,
                    SpecTypeId.Number,
                    false,
                    "TestText"
                )
            }.Where(p => p != null)
        .Where(p => family.FindParameter(p.ParameterName) != null)
        // .Where(p => check the value's datatype against the spectypeid)
        .ToArray();

        // Open family for editing (no transaction needed)
        var famDoc = doc.EditFamily(family);
        if (!famDoc.IsFamilyDocument) throw new ArgumentException("Document is not a family document.");
        if (famDoc.FamilyManager is null) throw new InvalidOperationException("Family documents FamilyManager is null.");

        _ = balloon.AddDebug(Balloon.Log.TEST, new StackFrame(), "Attempting to add parameters to family...");
        using var transFamily = new Transaction(famDoc, "Edit Family Document");
        _ = transFamily.Start();
        var (famEdit, famEditErr) = EditFamily(famDoc.FamilyManager, parameters, balloon);
        if (famEditErr is not null) {
            _ = transFamily.RollBack();
            var closed = famDoc.Close(false);
            if (!closed) _ = balloon.Add(Balloon.Log.ERR, "Failed to close family document after edit error.");
        }
        _ = transFamily.Commit(); // gotta see if this transaction hangline works

        _ = balloon.AddDebug(Balloon.Log.TEST, new StackFrame(), "Attempting to reload family into project...");
        using var trans = new Transaction(famDoc, "Edit Family Document");
        _ = trans.Start();
        var fam = famDoc.LoadFamily(doc, new FamilyOption());
        if (fam is not null) {
            _ = trans.RollBack();
            var closed = famDoc.Close(false);
            if (!closed) _ = balloon.Add(Balloon.Log.ERR, "Failed to close family document after load error.");
        }
        _ = trans.Commit();
        return fam;



    }

    static Result<bool> EditFamily(
        FamilyManager fm,
        FamilyParameterInfo[] parameters,
        Balloon balloon = null
    ) {
        try {
            var paramResults = AddParameters(fm, parameters);
            foreach (var (param, err) in paramResults.Values) {
                _ = err is not null || fm.get_Parameter(param.Definition.Name) is null // TODO: need to check this logic, idk if get_parameter even returns null
                    ? (balloon?.AddDebug(Balloon.Log.INFO, new StackFrame(),
                        $"Parameter '{param.Definition.Name}' added successfully."))
                    : (balloon?.AddDebug(Balloon.Log.ERR, new StackFrame(),
                        $"Failed to add parameter '{param.Definition.Name}': {err.Message}"));
            }

            if (paramResults.Values.Count() <= 0) return false; // TODO: handle this path better?  what to do about empty parameters arrays?
            return true;
        } catch (Exception ex) {
            return ex;
        }
    }
}


class FamilyOption : IFamilyLoadOptions {
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