// TODO: Migrate this!!!!!!!!!!

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddFamilyParamsOperation : IOperation<AddFamilyParamsSettings> {
    public AddFamilyParamsSettings Settings { get; set; }

    // change this to type later probably after seeing if looping through the types isa ctually necessary
    public OperationType Type => OperationType.Type;
    public string Name => "Add Family Parameters";

    public string Description => "Add Family Parameters to the family";

    public void Execute(Document doc) => AddFamilyParams(doc, this.Settings.FamilyParamData);

    public static void AddFamilyParams(
        Document famDoc,
        List<FamilyParamDataRecord> parameters
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));

        var fm = famDoc.FamilyManager;

        foreach (var param in parameters) {
            try {
                var parameter = fm.FindParameter(param.Name);
                parameter ??= fm.AddParameter(param.Name, param.PropertiesGroup, param.DataType, param.IsInstance);
                _ = fm.SetValueStrict(parameter, param.Value); // add null check?
            } catch (Exception ex) {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}

public class AddFamilyParamsSettings {
    [Description(
        "Overwrite a family's existing parameter value/s if they already exist. Note: already places family instances' values will remain unchanged.")]
    [Required]
    public bool OverrideExistingValues { get; set; } = true;

    public List<FamilyParamDataRecord> FamilyParamData { get; set; } = [];
}

public record FamilyParamDataRecord {
    public string Name { get; init; }
    public ForgeTypeId PropertiesGroup { get; init; } // must find how to default to other
    public ForgeTypeId DataType { get; init; }
    public bool IsInstance { get; init; } = true;
    public object Value { get; init; }
}