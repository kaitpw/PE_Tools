using Newtonsoft.Json;
using Nice3point.Revit.Extensions;
using PeServices.Storage.Core.Json.SchemaProviders;

namespace PeServices.Storage.Core.Json.Converters;

/// <summary>
///     JSON converter for ForgeTypeId that serializes to/from human-readable labels with discipline information.
///     For writing: converts ForgeTypeId to display name with discipline (e.g., "Length (Common)", "Other")
///     For reading: attempts to find matching ForgeTypeId from known SpecTypeId/GroupTypeId constants,
///     automatically strips discipline suffix if present, falls back to creating a new ForgeTypeId from the TypeId string if not found.
///     Special case: "Other" maps to an empty ForgeTypeId (new ForgeTypeId("")).
///     Example JSON serialization:
///     <code>   
/// {
///   "DataType": "Length (Common)",
///   "PropertiesGroup": "Other"
/// }
/// </code>
/// </summary>
public class ForgeTypeIdConverter : JsonConverter<ForgeTypeId> {
    private static readonly Lazy<Dictionary<string, ForgeTypeId>> _labelMap = new(BuildLabelMap);

    public override void WriteJson(JsonWriter writer, ForgeTypeId value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        // Special case: Empty ForgeTypeId (or empty TypeId string) represents "Other" in Revit UI
        if (string.IsNullOrEmpty(value.TypeId)) {
            writer.WriteValue("Other");
            return;
        }

        // Try to get a human-readable label with discipline; fall back to raw TypeId if labeling fails
        // (e.g., discipline types or malformed ForgeTypeIds can throw in LabelUtils)
        try {
            var label = value.ToLabel();
            var discipline = GetParentheticDiscipline(value);
            writer.WriteValue($"{label}{discipline}");
        } catch (Exception ex) {
            Debug.WriteLine($"Failed to get label for ForgeTypeId: {value.TypeId}\n\t error message: {ex.Message}");
            writer.WriteValue(value.TypeId);
        }
    }

    public static string GetParentheticDiscipline(ForgeTypeId spec) {
        if (!UnitUtils.IsMeasurableSpec(spec)) return string.Empty;
        var disciplineId = UnitUtils.GetDiscipline(spec);
        var disciplineLabel = LabelUtils.GetLabelForDiscipline(disciplineId);
        return !string.IsNullOrEmpty(disciplineLabel) ? $" ({disciplineLabel})" : string.Empty;
    }

    public override ForgeTypeId ReadJson(JsonReader reader,
        Type objectType,
        ForgeTypeId existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Null) return null;

        var input = reader.Value?.ToString();
        if (string.IsNullOrWhiteSpace(input)) return null;

        // Special case: "Other" in Revit UI maps to an empty ForgeTypeId
        if (input.Equals("Other", StringComparison.OrdinalIgnoreCase)) return new ForgeTypeId("");

        // Strip discipline suffix if present (e.g., "Length (Common)" -> "Length")
        var labelOnly = StripDisciplineSuffix(input);

        // First, try to find by label (most common case when reading JSON written by this converter)
        if (_labelMap.Value.TryGetValue(labelOnly, out var forgeTypeId)) return forgeTypeId;

        // If not found by label, check if the input is a valid TypeId format (starts with "autodesk.")
        // If so, create a new ForgeTypeId from it
        if (labelOnly.StartsWith("autodesk.", StringComparison.OrdinalIgnoreCase)) return new ForgeTypeId(labelOnly);

        // If we get here, the input is neither a known label nor a valid TypeId format
        // For backwards compatibility with legacy JSON files, return null for invalid values
        // This allows the property to use its default value (null or empty ForgeTypeId)
        return null;
    }

    /// <summary>
    ///     Strips discipline suffix from formatted spec names.
    ///     Example: "Length (Common)" -> "Length"
    /// </summary>
    private static string StripDisciplineSuffix(string input) {
        var parenIndex = input.LastIndexOf('(');
        if (parenIndex > 0 && input.TrimEnd().EndsWith(")")) {
            return input[..parenIndex].Trim();
        }

        return input;
    }

    /// <summary>
    ///     Builds a map of labels to ForgeTypeId instances by reflecting over SpecTypeId and its nested classes,
    ///     as well as GroupTypeId. Uses LabelUtils to get human-readable labels for each ForgeTypeId.
    /// </summary>
    private static Dictionary<string, ForgeTypeId> BuildLabelMap() {
        var propertyGroups = new PropertyGroupNamesProvider().GetLabelMap();
        var specTypes = new SpecNamesProvider().GetLabelMap();
        return propertyGroups
            .Concat(specTypes)
            .ToDictionary(pair => pair.label, pair => pair.value);
    }
}