using Newtonsoft.Json;
using PeServices.Storage.Core.Json.Converters;

/// <summary>
///     Helper class for identifying ForgeTypeId-related types and converters.
///     Used by both the converter and schema processor to maintain consistency.
/// </summary>
public static class ForgeTypeIdJsonHelper {
    /// <summary>
    ///     The name of the ForgeTypeIdConverter class.
    /// </summary>
    public const string ConverterTypeName = nameof(ForgeTypeIdConverter);

    private static readonly Type ForgeTypeIdType = Type.GetType("Autodesk.Revit.DB.ForgeTypeId, RevitAPI")
                                                   ?? Type.GetType("Autodesk.Revit.DB.ForgeTypeId");

    private static readonly Type ForgeTypeIdConverterType = typeof(ForgeTypeIdConverter);

    /// <summary>
    ///     Checks if a property is a ForgeTypeId property or uses ForgeTypeIdConverter.
    /// </summary>
    public static bool IsForgeTypeIdProperty(PropertyInfo property) {
        // Check if property type is ForgeTypeId
        if (ForgeTypeIdType != null && property.PropertyType == ForgeTypeIdType) return true;
        // Fallback: check by type name
        if (property.PropertyType != null && property.PropertyType.Name == "ForgeTypeId") return true;

        // Check if property has JsonConverter attribute for ForgeTypeIdConverter
        var jsonConverterAttr = property.GetCustomAttribute<JsonConverterAttribute>();
        if (jsonConverterAttr != null) {
            var converterType = jsonConverterAttr.ConverterType;

            if (converterType != null) {
                // Check if converter type matches ForgeTypeIdConverter exactly
                if (converterType == ForgeTypeIdConverterType) return true;
                // Fallback: check by type name (for cases where types are from different assemblies)
                if (converterType.Name == ConverterTypeName) return true;
            }
        }

        return false;
    }
}