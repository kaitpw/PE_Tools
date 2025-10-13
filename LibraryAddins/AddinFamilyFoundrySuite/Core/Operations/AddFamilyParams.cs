// TODO: Migrate this!!!!!!!!!!

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddAndGlobalSetFamilyParams : IOperation<AddAndGlobalSetFamilyParamsSettings> {
    public AddAndGlobalSetFamilyParamsSettings Settings { get; set; }

    // change this to type later probably after seeing if looping through the types isa ctually necessary
    public OperationType Type => OperationType.Type;
    public string Name => "Add Family Parameters";

    public string Description => "Add Family Parameters to the family";

    public void Execute(Document doc) => AddFamilyParams(doc, this.Settings.FamilyParamData);

    public static void AddFamilyParams(
        Document famDoc,
        List<FamilyParamModel> parameters
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");
        if (parameters is null || parameters.Where(p => p is null).Any())
            throw new ArgumentNullException(nameof(parameters));

        var fm = famDoc.FamilyManager;

        foreach (var p in parameters) {
            var parameter = famDoc.AddFamilyParameter(p.Name, p.PropertiesGroup, p.DataType, p.IsInstance);
            if (p.GlobalValue is not null)
                _ = fm.SetValueStrict(parameter, p.GlobalValue);
        }
    }
}


public class AddAndGlobalSetFamilyParamsSettings {
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
    [Required]
    public bool OverrideExistingValues { get; set; } = true;

    public List<FamilyParamModel> FamilyParamData { get; set; } = [];
}

public record FamilyParamModel {
    public string Name { get; init; }
    public ForgeTypeId PropertiesGroup { get; init; } // must find how to default to other
    public ForgeTypeId DataType { get; init; }
    public bool IsInstance { get; init; } = true;
    public object GlobalValue { get; init; }
}