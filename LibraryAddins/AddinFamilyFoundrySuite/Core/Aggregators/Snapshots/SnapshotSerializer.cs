using AddinFamilyFoundrySuite.Core.Snapshots;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PeServices.Storage.Core.Json.ContractResolvers;
using PeServices.Storage.Core.Json.Converters;

namespace AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;

public static class SnapshotSerializer {
    private static readonly JsonSerializerSettings _settings = new() {
        Formatting = Formatting.Indented,
        ContractResolver = new RequiredAwareContractResolver(),
        Converters = [new ForgeTypeIdConverter(), new StringEnumConverter()]
    };

    // JSON
    public static string ToJson(this List<ParamSnapshot> snapshots) =>
        JsonConvert.SerializeObject(snapshots ?? [], _settings);

    public static string ToJson(this List<RefPlaneSpec> specs) =>
        JsonConvert.SerializeObject(specs ?? [], _settings);


    public static List<ParamSnapshot> FromJson(string json) =>
        JsonConvert.DeserializeObject<List<ParamSnapshot>>(json ?? string.Empty, _settings) ?? [];

    // CSV (with type columns)
    public static string ToCsv(this List<ParamSnapshot> snapshots) {
        snapshots ??= [];

        var typeNames = snapshots
            .SelectMany(s => s.ValuesPerType.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = new List<string>();
        lines.Add(string.Join(",", new[] { "Name", "IsInstance", "IsProjectParameter", "PropertiesGroup", "DataType", "Formula" }
            .Concat(typeNames)
            .Select(EscapeCsvField)));

        foreach (var s in snapshots.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)) {
            var fixedCols = new[] {
                s.Name, s.IsInstance.ToString(), s.IsProjectParameter.ToString(),
                SerializeForgeTypeIdLabel(s.PropertiesGroup),
                SerializeForgeTypeIdLabel(s.DataType), s.Formula ?? string.Empty
            };

            var valueCols = typeNames
                .Select(typeName =>
                    s.ValuesPerType.TryGetValue(typeName, out var v) ? v ?? string.Empty : string.Empty);

            lines.Add(string.Join(",", fixedCols.Concat(valueCols).Select(EscapeCsvField)));
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static List<ParamSnapshot> FromCsv(string csv) {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        var lines = csv
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (lines.Count < 2)
            return [];

        var header = ParseCsvLine(lines[0]);

        // Support both old format (5 columns) and new format (6 columns with IsProjectParameter)
        var hasProjectParamColumn = header.Count >= 6 && header[2] == "IsProjectParameter";
        var minColumns = hasProjectParamColumn ? 6 : 5;

        if (header.Count < minColumns)
            throw new InvalidOperationException(
                "CSV header must include Name,IsInstance,[IsProjectParameter,]PropertiesGroup,DataType,Formula");

        var typeColumns = header.Skip(minColumns).ToList();
        var snapshots = new List<ParamSnapshot>();

        foreach (var line in lines.Skip(1)) {
            var fields = ParseCsvLine(line);
            if (fields.Count < minColumns)
                continue;

            var name = fields[0];
            var isInstance = bool.TryParse(fields[1], out var parsedIsInstance) && parsedIsInstance;

            bool isProjectParameter;
            ForgeTypeId propertiesGroup;
            ForgeTypeId dataType;
            string formula;

            if (hasProjectParamColumn) {
                isProjectParameter = bool.TryParse(fields[2], out var parsedIsProjectParam) && parsedIsProjectParam;
                propertiesGroup = DeserializeForgeTypeIdLabel(fields[3]) ?? new ForgeTypeId("");
                dataType = DeserializeForgeTypeIdLabel(fields[4]) ?? SpecTypeId.String.Text;
                formula = string.IsNullOrWhiteSpace(fields[5]) ? null : fields[5];
            } else {
                isProjectParameter = false; // Old format doesn't have this column
                propertiesGroup = DeserializeForgeTypeIdLabel(fields[2]) ?? new ForgeTypeId("");
                dataType = DeserializeForgeTypeIdLabel(fields[3]) ?? SpecTypeId.String.Text;
                formula = string.IsNullOrWhiteSpace(fields[4]) ? null : fields[4];
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < typeColumns.Count; i++) {
                var idx = minColumns + i;
                var v = idx < fields.Count ? fields[idx] : string.Empty;
                values[typeColumns[i]] = string.IsNullOrWhiteSpace(v) ? null : v;
            }

            snapshots.Add(new ParamSnapshot {
                Name = name,
                IsInstance = isInstance,
                IsProjectParameter = isProjectParameter,
                PropertiesGroup = propertiesGroup,
                DataType = dataType,
                Formula = formula,
                ValuesPerType = values
            });
        }

        return snapshots;
    }

    private static string SerializeForgeTypeIdLabel(ForgeTypeId id) {
        var json = JsonConvert.SerializeObject(id, _settings);
        return json.Trim().Trim('"');
    }

    private static ForgeTypeId DeserializeForgeTypeIdLabel(string label) {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        // Convert to JSON string literal and let ForgeTypeIdConverter handle label->id
        var json = "\"" + EscapeJsonString(label) + "\"";
        return JsonConvert.DeserializeObject<ForgeTypeId>(json, _settings);
    }

    private static string EscapeCsvField(string field) {
        field ??= string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";

        return field;
    }

    private static List<string> ParseCsvLine(string line) {
        var result = new List<string>();
        if (line is null)
            return result;

        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++) {
            var c = line[i];

            if (inQuotes) {
                if (c == '"') {
                    if (i + 1 < line.Length && line[i + 1] == '"') {
                        _ = sb.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                _ = sb.Append(c);
                continue;
            }

            if (c == ',') {
                result.Add(sb.ToString());
                _ = sb.Clear();
                continue;
            }

            if (c == '"') {
                inQuotes = true;
                continue;
            }

            _ = sb.Append(c);
        }

        result.Add(sb.ToString());
        return result;
    }

    private static string EscapeJsonString(string s) {
        if (s is null) return string.Empty;

        // minimal JSON string escaping
        return s
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    // RefPlaneSpec JSON


    // RefPlaneSpec CSV
    public static string ToCsv(this List<RefPlaneSpec> specs) {
        specs ??= [];

        var lines = new List<string>();
        lines.Add(string.Join(",", new[] { "Name", "AnchorName", "Placement", "Parameter", "Strength" }
            .Select(EscapeCsvField)));

        foreach (var s in specs.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) {
            var cols = new[] {
                s.Name ?? string.Empty,
                s.AnchorName ?? string.Empty,
                s.Placement.ToString(),
                s.Parameter ?? string.Empty,
                s.Strength.ToString()
            };
            lines.Add(string.Join(",", cols.Select(EscapeCsvField)));
        }

        return string.Join(Environment.NewLine, lines);
    }
}