using System.Reflection;
using Newtonsoft.Json;

namespace PeServices.Storage.Core;

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
    private static readonly Lazy<Dictionary<string, ForgeTypeId>> _labelMap = new(BuildLabelMap);

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

        var input = reader.Value?.ToString();
        if (string.IsNullOrWhiteSpace(input)) return null;

        // First, try to find by label (most common case when reading JSON written by this converter)
        if (_labelMap.Value.TryGetValue(input, out var forgeTypeId)) return forgeTypeId;

        // If not found by label, check if the input is a valid TypeId format (starts with "autodesk.")
        // If so, create a new ForgeTypeId from it
        if (input.StartsWith("autodesk.", StringComparison.OrdinalIgnoreCase)) {
            return new ForgeTypeId(input);
        }

        // If we get here, the input is neither a known label nor a valid TypeId format
        // This shouldn't happen with properly serialized JSON, but throw a helpful error
        throw new JsonSerializationException(
            $"Cannot convert '{input}' to ForgeTypeId. Expected a label (e.g., 'Text', 'Length') or a TypeId string (e.g., 'autodesk.spec:string').");
    }

    /// <summary>
    ///     Builds a map of labels to ForgeTypeId instances by reflecting over SpecTypeId and its nested classes,
    ///     as well as GroupTypeId. Uses LabelUtils to get human-readable labels for each ForgeTypeId.
    /// </summary>
    private static Dictionary<string, ForgeTypeId> BuildLabelMap() {
        var map = new Dictionary<string, ForgeTypeId>(StringComparer.OrdinalIgnoreCase);

        // Process SpecTypeId and its nested classes
        var specTypeIdType = typeof(SpecTypeId);
        AddPropertiesToLabelMap(specTypeIdType, map);
        var nestedTypes = specTypeIdType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);
        foreach (var nestedType in nestedTypes) AddPropertiesToLabelMap(nestedType, map);

        // Process GroupTypeId
        var groupTypeIdType = typeof(GroupTypeId);
        AddPropertiesToLabelMap(groupTypeIdType, map);

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

            // Try to get label for spec type
            string label = null;
            try {
                label = LabelUtils.GetLabelForSpec(value);
            } catch {
                // Not a spec type, try group type
                try {
                    label = LabelUtils.GetLabelForGroup(value);
                } catch {
                    // If both fail, use TypeId as fallback
                    label = value.TypeId;
                }
            }

            // Add to map (case-insensitive), but don't overwrite if already exists (first wins)
            if (!string.IsNullOrEmpty(label) && !map.ContainsKey(label)) {
                map[label] = value;
            }

            // Also add TypeId -> ForgeTypeId mapping for backwards compatibility
            if (!string.IsNullOrEmpty(value.TypeId) && !map.ContainsKey(value.TypeId)) {
                map[value.TypeId] = value;
            }
        }
    }
}

/// <summary>
///     Helper class for identifying ForgeTypeId-related types and converters.
///     Used by both the converter and schema processor to maintain consistency.
/// </summary>
public static class ForgeTypeIdJsonHelper {
    private static readonly Type ForgeTypeIdType = Type.GetType("Autodesk.Revit.DB.ForgeTypeId, RevitAPI") 
                                                    ?? Type.GetType("Autodesk.Revit.DB.ForgeTypeId");
    private static readonly Type ForgeTypeIdConverterType = typeof(ForgeTypeIdConverter);

    /// <summary>
    ///     The name of the ForgeTypeIdConverter class.
    /// </summary>
    public const string ConverterTypeName = nameof(ForgeTypeIdConverter);

    /// <summary>
    ///     Checks if a property is a ForgeTypeId property or uses ForgeTypeIdConverter.
    /// </summary>
    public static bool IsForgeTypeIdProperty(PropertyInfo property) {
        System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Checking property '{property.Name}' of type '{property.PropertyType?.FullName ?? "null"}'");
        
        // Check if property type is ForgeTypeId
        if (ForgeTypeIdType != null && property.PropertyType == ForgeTypeIdType) {
            System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Property '{property.Name}' matches ForgeTypeIdType exactly");
            return true;
        }
        // Fallback: check by type name
        if (property.PropertyType != null && property.PropertyType.Name == "ForgeTypeId") {
            System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Property '{property.Name}' matches ForgeTypeId by name");
            return true;
        }

        // Check if property has JsonConverter attribute for ForgeTypeIdConverter
        var jsonConverterAttr = property.GetCustomAttribute<JsonConverterAttribute>();
        if (jsonConverterAttr != null) {
            System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Property '{property.Name}' has JsonConverter attribute");
            var converterType = jsonConverterAttr.ConverterType;
            System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Converter type: {converterType?.FullName ?? "null"}");
            System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Expected converter type: {ForgeTypeIdConverterType.FullName}");
            
            if (converterType != null) {
                // Check if converter type matches ForgeTypeIdConverter exactly
                if (converterType == ForgeTypeIdConverterType) {
                    System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Property '{property.Name}' converter matches exactly");
                    return true;
                }
                // Fallback: check by type name (for cases where types are from different assemblies)
                System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Converter name: '{converterType.Name}', Expected: '{ConverterTypeName}'");
                if (converterType.Name == ConverterTypeName) {
                    System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Property '{property.Name}' converter matches by name");
                    return true;
                }
            }
        } else {
            System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Property '{property.Name}' has no JsonConverter attribute");
        }

        System.Diagnostics.Debug.WriteLine($"[ForgeTypeIdJsonHelper] Property '{property.Name}' is NOT a ForgeTypeId property");
        return false;
    }
}

