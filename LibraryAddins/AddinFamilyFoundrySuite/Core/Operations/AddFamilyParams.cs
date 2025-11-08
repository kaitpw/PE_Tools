using PeExtensions.FamDocument;
using AddinFamilyFoundrySuite.Core.OperationSettings;

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