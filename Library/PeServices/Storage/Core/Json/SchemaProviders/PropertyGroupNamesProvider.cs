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
        var labels = new List<(string, ForgeTypeId)>();
        foreach (var property in properties) {
            if (property.PropertyType != typeof(ForgeTypeId)) continue;
            var value = property.GetValue(null) as ForgeTypeId;
            if (value == null) continue;
            labels.Add((value.ToLabel(), value));
        }
        return labels.Distinct();
    }
}