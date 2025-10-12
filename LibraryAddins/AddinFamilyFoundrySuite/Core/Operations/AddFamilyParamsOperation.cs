// TODO: Migrate this!!!!!!!!!!

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public static class AddFamilyParamsOperation {
    public static List<Result<FamilyParameter>> AddFamilyParams(
        Document famDoc,
        FamilyParamInfo[] parameters
    ) {
        if (famDoc is null) throw new ArgumentNullException(nameof(famDoc));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var fm = famDoc.FamilyManager;
        var results = new List<Result<FamilyParameter>>();
        foreach (FamilyType type in fm.Types) {
            fm.CurrentType = type;
            foreach (var param in parameters) {
                try {
                    var parameter = fm.FindParameter(param.Name);
                    parameter ??= fm.AddParameter(param.Name, param.Group, param.Category, param.IsInstance);
                    results.Add(parameter);
                } catch (Exception ex) {
                    results.Add(ex);
                }
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

public class AddParamsFP {
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
    [Required]
    public bool OverrideExistingValues { get; set; } = true;
    // public FpRecoverFromErrorSettings RecoverFromErrorSettings { get; init; } = new();
    //
    // public class FpRecoverFromErrorSettings {
    //     public bool DangerouslyReplaceParameterWithMatchingName;
    // }
}