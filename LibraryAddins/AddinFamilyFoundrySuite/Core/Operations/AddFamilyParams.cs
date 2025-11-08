using PeExtensions.FamDocument;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PeServices.Storage.Core.Json.Converters;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddFamilyParams(AddFamilyParamsSettings settings)
    : DocOperation<AddFamilyParamsSettings>(settings) {
    public override string Description =>
        "Add Family Parameters and set the value for each family type to the same value.";

    public override OperationLog Execute(FamilyDocument doc) {
        foreach (var p in this.Settings.FamilyParamData)
            _ = doc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
        return null;
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