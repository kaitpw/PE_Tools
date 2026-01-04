

using Nice3point.Revit.Extensions;
using PeServices.Storage.Core.Json.Converters;
using PeServices.Storage.Core.Json.SchemaProcessors;
namespace PeServices.Storage.Core.Json.SchemaProviders;

public class SpecNamesProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() =>
        this.GetLabelMap().Select(s => s.label);

    private static string FormatSpecWithDiscipline(ForgeTypeId spec) {
        var label = spec.ToLabel();
        var discipline = ForgeTypeIdConverter.GetParentheticDiscipline(spec);
        return $"{label}{discipline}";
    }


    public IEnumerable<(string label, ForgeTypeId value)> GetLabelMap() =>
        SpecUtils.GetAllSpecs().Select(s => (FormatSpecWithDiscipline(s), s)).Distinct();
}