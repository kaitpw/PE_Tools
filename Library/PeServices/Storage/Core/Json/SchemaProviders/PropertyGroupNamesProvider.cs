using Nice3point.Revit.Extensions;
using PeServices.Storage.Core.Json.SchemaProcessors;
namespace PeServices.Storage.Core.Json.SchemaProviders;

public class PropertyGroupNamesProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() {
        var labelMap = this.GetLabelMap();
        return labelMap.Select(s => s.label);
    }

    public IEnumerable<(string label, ForgeTypeId value)> GetLabelMap() {
        var properties = typeof(GroupTypeId).GetProperties(BindingFlags.Public | BindingFlags.Static);
        var labelMap = new Dictionary<string, ForgeTypeId>();

        foreach (var property in properties) {
            if (property.PropertyType != typeof(ForgeTypeId)) continue;
            var value = property.GetValue(null) as ForgeTypeId;
            if (value == null) continue;

            var label = value.ToLabel();
            // Skip duplicates - keep first occurrence
            if (!labelMap.ContainsKey(label)) {
                labelMap[label] = value;
            }
        }

        return labelMap.Select(kvp => (kvp.Key, kvp.Value));
    }
}