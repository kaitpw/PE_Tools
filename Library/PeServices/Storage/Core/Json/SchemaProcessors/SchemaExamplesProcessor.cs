using Newtonsoft.Json;
using NJsonSchema.Generation;

namespace PeServices.Storage.Core.Json.SchemaProcessors;

/// <summary>
///     Provider interface for runtime schema examples.
///     Implement this to supply autocomplete suggestions for a property.
/// </summary>
public interface ISchemaExamplesProvider {
    IEnumerable<string> GetExamples();
}

/// <summary>
///     Marks a property to receive runtime examples in the JSON schema for LSP autocomplete.
///     Unlike EnumConstraintAttribute, examples are suggestions only - any value is valid.
///     Usage: [SchemaExamples(typeof(MyProvider))]
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SchemaExamplesAttribute : Attribute {
    public Type ProviderType { get; }

    public SchemaExamplesAttribute(Type providerType) {
        if (!typeof(ISchemaExamplesProvider).IsAssignableFrom(providerType))
            throw new ArgumentException(
                $"Provider type must implement {nameof(ISchemaExamplesProvider)}", nameof(providerType));
        this.ProviderType = providerType;
    }
}

/// <summary>
///     Schema processor that injects runtime examples into properties marked with SchemaExamplesAttribute.
///     Examples appear as autocomplete suggestions in LSP without enforcing validation.
///     Handles both direct string properties and array/list properties (adds examples to item schema).
/// </summary>
public class SchemaExamplesProcessor : ISchemaProcessor {
    public void Process(SchemaProcessorContext context) {
        if (!context.ContextualType.Type.IsClass) return;

        foreach (var property in context.ContextualType.Type.GetProperties()) {
            var attr = property.GetCustomAttribute<SchemaExamplesAttribute>();
            if (attr == null) continue;

            var propertyName = GetJsonPropertyName(property);
            if (!context.Schema.Properties.TryGetValue(propertyName, out var propSchema)) continue;

            try {
                var provider = (ISchemaExamplesProvider)Activator.CreateInstance(attr.ProviderType);
                var examples = provider.GetExamples().ToList();

                // For array/list types, add examples to the item schema for element autocomplete
                if (propSchema.Item != null) {
                    propSchema.Item.ExtensionData ??= new Dictionary<string, object>();
                    propSchema.Item.ExtensionData["examples"] = examples;
                } else {
                    // For direct string properties
                    propSchema.ExtensionData ??= new Dictionary<string, object>();
                    propSchema.ExtensionData["examples"] = examples;
                }
            } catch {
                // Fail silently - examples are a nicety, not critical
            }
        }
    }

    private static string GetJsonPropertyName(PropertyInfo property) {
        var jsonPropertyAttr = property.GetCustomAttribute<JsonPropertyAttribute>();
        return jsonPropertyAttr?.PropertyName ?? property.Name;
    }
}

