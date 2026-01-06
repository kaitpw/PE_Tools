using AddinFamilyFoundrySuite.Core.Snapshots;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Nice3point.Revit.Extensions;
using PeServices.Storage.Core.Json.ContractResolvers;
using PeServices.Storage.Core.Json.Converters;
using PeServices.Storage.Core.Json.SchemaProviders;

namespace AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;

public static class SnapshotSerializer {
    private static readonly JsonSerializerSettings _settings = new() {
        Formatting = Formatting.Indented,
        ContractResolver = new RequiredAwareContractResolver(),
        Converters = [new StringEnumConverter()]
    };
    private static readonly string[] CsvHeaders = ["Name", "IsInstance", "IsProjectParameter", "PropertiesGroup", "DataType", "Formula"];

    // JSON

    public static string ToJson(this List<ParamSnapshot> snapshots) {
        snapshots ??= [];
        var sorted = snapshots.Select(s => s with {
            ValuesPerType = new Dictionary<string, string>(
                s.ValuesPerType.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase),
                StringComparer.Ordinal
            )
        }).ToList();
        return JsonConvert.SerializeObject(sorted, _settings);
    }

    public static string ToJson(this List<RefPlaneSpec> specs) =>
        JsonConvert.SerializeObject(specs ?? [], _settings);

    // CSV (with type columns)
    public static string ToCsv(this List<ParamSnapshot> snapshots) {
        snapshots ??= [];

        var typeNames = snapshots
            .SelectMany(s => s.ValuesPerType.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string> {
            string.Join(",",CsvHeaders.Concat(typeNames).Select(EscapeCsvField))
        };

        foreach (var s in snapshots.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)) {
            var fixedCols = new[] {
                s.Name,
                s.IsInstance.ToString(),
                s.IsProjectParameter.ToString(),
                _groupTypeLabelMap.TryGetValue(s.PropertiesGroup, out var groupLabel) ? groupLabel : $"Unknown Group Type: {s.PropertiesGroup}",
                _specTypeLabelMap.TryGetValue(s.DataType, out var specLabel) ? specLabel : $"Unknown Spec Type: {s.DataType}",
                s.Formula ?? string.Empty
            };

            var valueCols = typeNames
                .Select(typeName =>
                    s.ValuesPerType.TryGetValue(typeName, out var v) ? v ?? string.Empty : string.Empty);

            lines.Add(string.Join(",", fixedCols.Concat(valueCols).Select(EscapeCsvField)));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static Dictionary<ForgeTypeId, string> _groupTypeLabelMap => PropertyGroupNamesProvider.GetLabelMap().ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
    private static Dictionary<ForgeTypeId, string> _specTypeLabelMap => SpecNamesProvider.GetLabelMap().ToDictionary(kvp => kvp.Value, kvp => kvp.Key);


    private static string EscapeCsvField(string field) {
        field ??= string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";

        return field;
    }


    // RefPlaneSpec CSV
    public static string ToCsv(this List<RefPlaneSpec> specs) {
        specs ??= [];

        var lines = new List<string> {
            string.Join(",", new[] { "Name", "AnchorName", "Placement", "Parameter", "Strength" }
            .Select(EscapeCsvField))
        };

        foreach (var s in specs.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) {
            var cols = new[] {
                s.Name ?? string.Empty, s.AnchorName ?? string.Empty, s.Placement.ToString(),
                s.Parameter ?? string.Empty, s.Strength.ToString()
            };
            lines.Add(string.Join(",", cols.Select(EscapeCsvField)));
        }

        return string.Join(Environment.NewLine, lines);
    }
}