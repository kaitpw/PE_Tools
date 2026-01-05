using NJsonSchema;
using NJsonSchema.Generation;

namespace PeServices.Storage.Core.Json.SchemaProcessors;

/// <summary>
///     Schema processor that enforces mutual exclusivity between properties using oneOf.
///     Properties marked with [MutuallyExclusive] will generate a oneOf constraint
///     ensuring only one of the specified properties can be present.
/// </summary>
public class MutuallyExclusiveSchemaProcessor : ISchemaProcessor {
    public void Process(SchemaProcessorContext context) {
        var type = context.ContextualType.Type;
        
        // Look for MutuallyExclusiveAttribute on the type
        var mutuallyExclusiveAttr = type.GetCustomAttributes(typeof(MutuallyExclusiveAttribute), false)
            .Cast<MutuallyExclusiveAttribute>()
            .FirstOrDefault();

        if (mutuallyExclusiveAttr == null) return;

        var schema = context.Schema;
        var propertyGroups = mutuallyExclusiveAttr.PropertyGroups;

        // Create oneOf schemas for each property group
        var oneOfSchemas = new List<JsonSchema>();

        foreach (var propertyName in propertyGroups) {
            if (!schema.Properties.ContainsKey(propertyName)) continue;

            var subSchema = new JsonSchema {
                Type = JsonObjectType.Object,
                AllowAdditionalProperties = false
            };

            // Add this property as required
            subSchema.RequiredProperties.Add(propertyName);
            
            // Copy the property definition
            subSchema.Properties[propertyName] = schema.Properties[propertyName];

            // Add all other properties from the parent (except the mutually exclusive ones)
            foreach (var kvp in schema.Properties) {
                if (propertyGroups.Contains(kvp.Key)) {
                    // For mutually exclusive properties, explicitly disallow them
                    if (kvp.Key != propertyName) {
                        subSchema.Properties[kvp.Key] = new JsonSchemaProperty {
                            Not = new JsonSchema { Type = JsonObjectType.None }
                        };
                    }
                } else {
                    // Copy non-exclusive properties as-is
                    subSchema.Properties[kvp.Key] = kvp.Value;
                    if (schema.RequiredProperties.Contains(kvp.Key))
                        subSchema.RequiredProperties.Add(kvp.Key);
                }
            }

            oneOfSchemas.Add(subSchema);
        }

        if (oneOfSchemas.Count > 0) {
            schema.OneOf.Clear();
            foreach (var subSchema in oneOfSchemas)
                schema.OneOf.Add(subSchema);
        }
    }
}

/// <summary>
///     Marks a type as having mutually exclusive properties.
///     Only one of the specified properties can be present in the JSON.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class MutuallyExclusiveAttribute : Attribute {
    public MutuallyExclusiveAttribute(params string[] propertyGroups) {
        this.PropertyGroups = propertyGroups;
    }

    public string[] PropertyGroups { get; }
}
