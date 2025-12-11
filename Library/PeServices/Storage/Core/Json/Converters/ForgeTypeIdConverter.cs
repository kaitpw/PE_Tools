using Newtonsoft.Json;
using Nice3point.Revit.Extensions;

namespace PeServices.Storage.Core.Json.Converters;

/// <summary>
///     JSON converter for ForgeTypeId that serializes to/from human-readable labels using LabelUtils.
///     For writing: converts ForgeTypeId to display name (e.g., "Length", "Dimensions", "Other")
///     For reading: attempts to find matching ForgeTypeId from known SpecTypeId/GroupTypeId constants,
///     falls back to creating a new ForgeTypeId from the TypeId string if not found.
///     Special case: "Other" maps to an empty ForgeTypeId (new ForgeTypeId("")).
///     Example JSON serialization:
///     <code>   
/// {
///   "DataType": "Length",
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
        if (string.IsNullOrEmpty(value.TypeId)) { // TODO: test this more, This is for GroupTypeId, but we need to handle all forgeTypeId cases
            writer.WriteValue("Other");
            return;
        }

        writer.WriteValue(value.ToLabel());
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

        // First, try to find by label (most common case when reading JSON written by this converter)
        if (_labelMap.Value.TryGetValue(input, out var forgeTypeId)) return forgeTypeId;

        // If not found by label, check if the input is a valid TypeId format (starts with "autodesk.")
        // If so, create a new ForgeTypeId from it
        if (input.StartsWith("autodesk.", StringComparison.OrdinalIgnoreCase)) return new ForgeTypeId(input);

        // If we get here, the input is neither a known label nor a valid TypeId format
        // For backwards compatibility with legacy JSON files, return null for invalid values
        // This allows the property to use its default value (null or empty ForgeTypeId)
        return null;
    }

    /// <summary>
    ///     Builds a map of labels to ForgeTypeId instances by reflecting over SpecTypeId and its nested classes,
    ///     as well as GroupTypeId. Uses LabelUtils to get human-readable labels for each ForgeTypeId.
    /// </summary>
    private static Dictionary<string, ForgeTypeId> BuildLabelMap() {
        var map = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase);

        // Process SpecTypeId and its nested classes
        AddPropertiesToLabelMap(typeof(SpecTypeId), map);
        typeof(SpecTypeId)
            .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
            .ToList()
            .ForEach(type => AddPropertiesToLabelMap(type, map));

        // Process GroupTypeId
        AddPropertiesToLabelMap(typeof(GroupTypeId), map);
        return map;
    }

    /// <summary>
    ///     Adds all static ForgeTypeId properties from a type to the label map.
    ///     For each ForgeTypeId, gets its label using LabelUtils and adds it to the map.
    /// </summary>
    private static void AddPropertiesToLabelMap(Type type, Dictionary<string, ForgeTypeId> map) {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);

        foreach (var property in properties) {
            if (property.PropertyType != typeof(ForgeTypeId)) continue;

            var value = property.GetValue(null) as ForgeTypeId;
            if (value == null) continue;

            // Get label using the appropriate LabelUtils method based on the source type
            string label;
            try {
                label = value.ToLabel();
            } catch {
                // Skip ForgeTypeIds that can't be labeled (e.g., SpecTypeId.Custom)
                // We only map ForgeTypeIds that have valid labels
                continue;
            }

            // Add to map (case-insensitive), but don't overwrite if already exists (first wins)
            if (!string.IsNullOrEmpty(label) && !map.ContainsKey(label)) map[label] = value;

            // Also add TypeId -> ForgeTypeId mapping for backwards compatibility
            if (!string.IsNullOrEmpty(value.TypeId) && !map.ContainsKey(value.TypeId)) map[value.TypeId] = value;
        }
    }
}