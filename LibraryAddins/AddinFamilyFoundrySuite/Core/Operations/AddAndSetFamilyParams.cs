using PeExtensions.FamDocument;
using PeServices.Storage.Core.Json.Converters;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddAndSetFamilyParams : OperationGroup<AddFamilyParamsSettings> {
    public AddAndSetFamilyParams(AddFamilyParamsSettings settings) : base(
        "Add Family Parameters and set their value OR formula.",
        InitializeOperations(settings)
    ) {
    }

    private static List<IOperation<AddFamilyParamsSettings>> InitializeOperations(
        AddFamilyParamsSettings settings
    ) {
        var operations = new List<IOperation<AddFamilyParamsSettings>>();
        var hasGlobalValues = settings.FamilyParamData.Any(p => p.GlobalValue is not null);
        if (hasGlobalValues) operations.Add(new SetParamValueAsValue(settings));
        operations.Add(new SetParamFormula(settings));
        return operations;
    }
}