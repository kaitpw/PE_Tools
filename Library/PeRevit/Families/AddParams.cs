using Json.Schema.Generation;
using PeRevit.Families;
using PeRevit.Ui;
using PeRevit.Families;
using PeServices.Aps;
using PeServices.Aps.Core;
using PeServices.Aps.Models;
using PeServices.Storage;

namespace PeRevit.Families;

public static class AddParams {
     public static List<Result<FamilyParameter>> Family(
        Document famDoc,
        FamilyParamInfo[] parameters,
        bool overrideExistingValue
    ) {
        var fm = famDoc.FamilyManager;
        var result = new List<Result<FamilyParameter>>();

        bool NoExistingParam(FamilyParamInfo p) {
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
                        case ElementId elementIdValue: // TODO: check if this works
                            fm.Set(param, elementIdValue);
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

    public static List<Result<FamilyParameter>> ParamSvc(
        Document famDoc,
        SharedParameterElement[] sharedParams
    ) {
        var fm = famDoc.FamilyManager;
        var results = new List<Result<FamilyParameter>>();

        foreach (var sharedParam in sharedParams) {
            // SharedParameterElement contains the ExternalDefinition we need
            // We can get it directly from the element, not through GetDefinition()
            try {
                // Add the shared parameter to the family using the shared parameter element
                // TODO: uncomment after first phase of tests
                // TODO: make a temporary shared param file!!!!!!!!!!!!!!!!!!! maybe?
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

    public record FamilyParamInfo {
    public string Name { get; init; }
    public ForgeTypeId Group { get; init; } // must find how to default to other
    public ForgeTypeId Category { get; init; }
    public bool IsInstance { get; init; } = true;
    public object Value { get; init; }
}
}