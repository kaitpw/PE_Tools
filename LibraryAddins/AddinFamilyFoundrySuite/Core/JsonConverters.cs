using Newtonsoft.Json;

namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     JSON converter for ForgeTypeId that serializes to/from human-readable labels using LabelUtils.
///     For writing: converts ForgeTypeId to display name (e.g., "Length", "Dimensions")
///     For reading: attempts to find matching ForgeTypeId from known SpecTypeId/GroupTypeId constants,
///     falls back to creating a new ForgeTypeId from the TypeId string if not found.
///     Example JSON serialization:
///     <code>
/// {
///   "DataType": "Length",
///   "PropertiesGroup": "Dimensions"
/// }
/// </code>
/// </summary>
public class ForgeTypeIdConverter : JsonConverter<ForgeTypeId> {
    private static readonly Lazy<Dictionary<string, ForgeTypeId>> _specTypeIdMap = new(BuildSpecTypeIdMap);

    public override void WriteJson(JsonWriter writer, ForgeTypeId value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        // Try to get a human-readable label using LabelUtils
        string label;
        try {
            label = LabelUtils.GetLabelForSpec(value);
        } catch {
            // Not a spec type, try group type
            try {
                label = LabelUtils.GetLabelForGroup(value);
            } catch {
                // If both fail, fall back to the TypeId string
                label = value.TypeId;
            }
        }

        writer.WriteValue(label);
    }

    public override ForgeTypeId ReadJson(JsonReader reader,
        Type objectType,
        ForgeTypeId existingValue,
        bool hasExistingValue,
        JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Null) return null;

        var typeId = reader.Value?.ToString();
        if (string.IsNullOrWhiteSpace(typeId)) return null;

        // Try to find a matching SpecTypeId property
        if (_specTypeIdMap.Value.TryGetValue(typeId, out var forgeTypeId)) return forgeTypeId;

        // If not found in SpecTypeId, create a new ForgeTypeId with the provided string
        return new ForgeTypeId(typeId);
    }

    /// <summary>
    ///     Builds a map of TypeId strings to ForgeTypeId instances by reflecting over SpecTypeId and its nested classes,
    ///     as well as other ID classes like GroupTypeId.
    /// </summary>
    private static Dictionary<string, ForgeTypeId> BuildSpecTypeIdMap() {
        var map = new Dictionary<string, ForgeTypeId>();

        // Process SpecTypeId and its nested classes
        var specTypeIdType = typeof(SpecTypeId);
        AddPropertiesToMap(specTypeIdType, map);
        var nestedTypes = specTypeIdType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
        foreach (var nestedType in nestedTypes) AddPropertiesToMap(nestedType, map);

        // Process GroupTypeId
        var groupTypeIdType = typeof(GroupTypeId);
        AddPropertiesToMap(groupTypeIdType, map);

        return map;
    }

    /// <summary>
    ///     Adds all static ForgeTypeId properties from a type to the map.
    /// </summary>
    private static void AddPropertiesToMap(Type type, Dictionary<string, ForgeTypeId> map) {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);

        foreach (var property in properties) {
            if (property.PropertyType != typeof(ForgeTypeId)) continue;

            var value = property.GetValue(null) as ForgeTypeId;
            if (value?.TypeId != null) map[value.TypeId] = value;
        }
    }
}