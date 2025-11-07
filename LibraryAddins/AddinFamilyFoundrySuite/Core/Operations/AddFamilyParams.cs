using PeServices.Storage.Core.Json.Converters;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AddinFamilyFoundrySuite.Core.Operations; 

public class AddFamilyParams : OperationGroup<AddFamilyParamsSettings> {
    public AddFamilyParams(AddFamilyParamsSettings settings) : base(
    "Make reference planes and dimensions for the family",
    InitializeOperations(settings)
) {
    }

    private static List<IOperation<AddFamilyParamsSettings>> InitializeOperations(
        AddFamilyParamsSettings settings
    ) {
        var hasGlobalValues = settings.FamilyParamData.Any(p => p.GlobalValue is not null);
        return hasGlobalValues ? [
            new AddAndSetValueAsValue(settings),
            new AddAndSetFormula(settings),
        ] : [
            new AddAndSetFormula(settings)
        ];
    }
}

public class AddFamilyParamsSettings : IOperationSettings {
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
    public bool OverrideExistingValues { get; init; } = true;

    public List<FamilyParamModel> FamilyParamData { get; init; } = [];
    public bool Enabled { get; init; } = true;
}

public record FamilyParamModel {
    [Required] public required string Name { get; init; }

    [JsonConverter(typeof(ForgeTypeIdConverter))]
    public ForgeTypeId PropertiesGroup { get; init; } = new("");

    [JsonConverter(typeof(ForgeTypeIdConverter))]
    [Required]
    public required ForgeTypeId DataType { get; init; }

    public bool IsInstance { get; init; } = true;
    public object GlobalValue { get; init; } = null;
    public string Formula { get; init; } = null;
}