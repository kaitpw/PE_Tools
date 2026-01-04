
using Nice3point.Revit.Extensions;
using PeServices.Storage.Core.Json.SchemaProcessors;
namespace PeServices.Storage.Core.Json.SchemaProviders;

public class SpecNamesProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() =>
        this.GetLabelMap().Select(s => s.label);

    private static string FormatSpecWithDiscipline(ForgeTypeId spec) {
        var label = spec.ToLabel();
        var discipline = GetParentheticDiscipline(spec);
        return $"{label}{discipline}";
    }

    private static string GetParentheticDiscipline(ForgeTypeId spec) {
        if (!UnitUtils.IsMeasurableSpec(spec)) return string.Empty;
        var disciplineId = UnitUtils.GetDiscipline(spec);
        var disciplineLabel = LabelUtils.GetLabelForDiscipline(disciplineId);
        return !string.IsNullOrEmpty(disciplineLabel) ? $" ({disciplineLabel})" : string.Empty;
    }

    public IEnumerable<(string label, ForgeTypeId value)> GetLabelMap() {
        var labelMap = new Dictionary<string, ForgeTypeId>();

        foreach (var spec in SpecUtils.GetAllSpecs()) {
            var label = FormatSpecWithDiscipline(spec);
            // Skip duplicates - keep first occurrence
            if (!labelMap.ContainsKey(label)) {
                labelMap[label] = spec;
            }
        }

        return labelMap.Select(kvp => (kvp.Key, kvp.Value));
    }
}