using Nice3point.Revit.Extensions;

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
            return fm.FindParameter(p.Name) == null;
        }

        parameters = parameters
            .Where(p => NoExistingParam(p) || overrideExistingValue)
            .ToArray();
        foreach (FamilyType type in fm.Types) {
            fm.CurrentType = type;
            foreach (var par in parameters) {
                try {
                    var parameter = fm.FindParameter(par.Name);
                    parameter ??= fm.AddParameter(par.Name, par.Group, par.Category, par.IsInstance);

                    fm.Set(parameter, par.Value);
                    result.Add(parameter);
                } catch (Exception ex) {
                    result.Add(ex);
                }
            }
        }

        return result;
    }

    public static List<Result<FamilyParameter>> ParamSvc(
        Document famDoc,
        List<SharedParameterElement> sharedParams
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