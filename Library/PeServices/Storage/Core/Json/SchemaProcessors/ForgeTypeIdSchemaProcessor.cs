using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Generation;

namespace PeServices.Storage.Core.Json.SchemaProcessors;

/// <summary>
///     Schema processor that converts ForgeTypeId properties to string schemas.
///     This is needed because ForgeTypeId is serialized as a string via JsonConverter,
///     but the schema generator treats it as an object by default.
/// </summary>
public class ForgeTypeIdSchemaProcessor : ISchemaProcessor {
    public void Process(SchemaProcessorContext context) {
        var type = context.ContextualType.Type;
        // Process classes and value types
        if (type.IsClass || type.IsValueType) ProcessTypeProperties(context, type);
        // Process array item schemas
        if (context.Schema.Item != null) ProcessItemSchema(context.Schema.Item, type);
    }

    private static void ProcessTypeProperties(SchemaProcessorContext context, Type typeToProcess) {
        var properties = typeToProcess.GetProperties();

        // Get the actual schema (handle references)
        var actualSchema = context.Schema;
        if (context.Schema.HasReference) actualSchema = context.Schema.Reference;

        foreach (var property in properties) {
            if (ForgeTypeIdJsonHelper.IsForgeTypeIdProperty(property)) {
                var propertyName = GetJsonPropertyName(property);

                if (actualSchema.Properties.TryGetValue(propertyName, out var propertySchema))
                    ConvertToStringSchema(propertySchema);
            }
        }
    }

    private static void ProcessItemSchema(JsonSchema itemSchema, Type originalType) {
        // If the original type is a generic collection, get the item type
        Type itemType = null;
        if (originalType.IsGenericType) {
            var genericArgs = originalType.GetGenericArguments();
            if (genericArgs.Length > 0) itemType = genericArgs[0];
        } else if (originalType.IsArray) itemType = originalType.GetElementType();

        // Process properties of the item type
        if (itemType != null && (itemType.IsClass || itemType.IsValueType)) {
            var properties = itemType.GetProperties();

            // Check if itemSchema has a reference (NJsonSchema may use references)
            var actualSchema = itemSchema;
            if (itemSchema.HasReference) actualSchema = itemSchema.Reference;


            foreach (var property in properties) {
                var isForgeTypeId = ForgeTypeIdJsonHelper.IsForgeTypeIdProperty(property);

                if (isForgeTypeId) {
                    var propertyName = GetJsonPropertyName(property);

                    if (actualSchema.Properties.TryGetValue(propertyName, out var propertySchema))
                        ConvertToStringSchema(propertySchema);
                }
            }
        }
    }

    private static void ConvertToStringSchema(JsonSchema propertySchema) {
        // If it has a reference, we need to modify the property schema itself, not the reference
        // Clear the reference first so we can set the type directly
        if (propertySchema.HasReference) propertySchema.Reference = null;

        // Check if property is nullable (has null in oneOf, which NJsonSchema adds for nullable reference types)
        var isNullable = propertySchema.OneOf.Any(s => s.Type == JsonObjectType.Null);

        // Clear oneOf entirely - we'll represent nullable as type array instead
        propertySchema.OneOf.Clear();

        // Convert to string schema: use type array for nullable, single type for non-nullable
        if (isNullable)
            propertySchema.Type = JsonObjectType.String | JsonObjectType.Null;
        else
            propertySchema.Type = JsonObjectType.String;
        propertySchema.Format = null;
        // Clear any object-related properties
        propertySchema.Properties.Clear();
        propertySchema.AdditionalPropertiesSchema = null;
    }

    private static string GetJsonPropertyName(PropertyInfo property) {
        var jsonPropertyNameAttr = property.GetCustomAttribute<JsonPropertyAttribute>();
        return jsonPropertyNameAttr?.PropertyName ?? property.Name;
    }
}