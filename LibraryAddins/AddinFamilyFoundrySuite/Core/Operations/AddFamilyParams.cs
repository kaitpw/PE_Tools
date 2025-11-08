using PeExtensions.FamDocument;
using PeServices.Storage.Core.Json.Converters;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddFamilyParams : OperationGroup<AddFamilyParamsSettings> {
    public AddFamilyParams(AddFamilyParamsSettings settings, bool addFormulas = true) : base(
        "Add Family Parameters and set their value OR formula.",
        InitializeOperations(settings, addFormulas)
    ) {
    }

    private static List<IOperation<AddFamilyParamsSettings>> InitializeOperations(
        AddFamilyParamsSettings settings,
        bool addFormulas
    ) {
        var operations = new List<IOperation<AddFamilyParamsSettings>> { new AddAllFamilyParams(settings) };
        var hasGlobalValues = settings.FamilyParamData.Any(p => p.GlobalValue is not null);
        if (hasGlobalValues) operations.Add(new AddAndSetValueAsValue(settings));
        if (addFormulas) operations.Add(new AddAndSetFormula(settings));
        return operations;
    }
}

public class AddAllFamilyParams(AddFamilyParamsSettings settings)
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