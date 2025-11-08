using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;

public class AddFamilyParamsSettings : IOperationSettings {
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
    public bool OverrideExistingValues { get; init; } = true;

    public List<FamilyParamModel> FamilyParamData { get; init; } = [];
    public bool Enabled { get; init; } = true;
}

public record FamilyParamModel {
    [Required] public required string Name { get; init; }

    public ForgeTypeId PropertiesGroup { get; init; } = new("");

    [Required]
    public required ForgeTypeId DataType { get; init; }

    public bool IsInstance { get; init; } = true;
    public object GlobalValue { get; init; } = null;
    public string Formula { get; init; } = null;
}