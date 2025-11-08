using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class DebugLogFamilyParams : DocOperation {
    public override string Description => "Debug log all family parameters and their values";

    public override OperationLog Execute(FamilyDocument doc) {
        var fm = doc.FamilyManager;
        Debug.WriteLine($"[DebugLogFamilyParams] Family: {doc.Document.Title}");
        Debug.WriteLine($"[DebugLogFamilyParams] Total family parameters: {fm.Parameters.Size}");

        foreach (var param in fm.GetParameters().Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))) {
            var value = fm.CurrentType?.AsValueString(param) ?? "null";
            Debug.WriteLine($"[DebugLogFamilyParams]   {param.Definition.Name} = {value}");
        }

        return null;
    }
}