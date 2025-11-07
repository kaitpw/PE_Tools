using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NJsonSchema.Validation;
using PeUtils.Files;

namespace PeServices.Storage.Core.Json.SchemaProcessors;

/// <summary>
///     Simple schema processor that adds enum constraints for properties marked with EnumConstraintAttribute
/// </summary>
public class EnumConstraintSchemaProcessor : ISchemaProcessor {
    public void Process(SchemaProcessorContext context) {
        if (context.ContextualType.Type.IsClass) {
            foreach (var property in context.ContextualType.Type.GetProperties()) {
                var attribute = property.GetCustomAttribute<EnumConstraintAttribute>();
                if (attribute != null) {
                    var propertyName = GetJsonPropertyName(property);
                    if (context.Schema.Properties.TryGetValue(propertyName, out var propertySchema)) {
                        propertySchema.Enumeration.Clear();
                        foreach (var value in attribute.Values) propertySchema.Enumeration.Add(value);
                    }
                }
            }
        }
    }

    private static string GetJsonPropertyName(PropertyInfo property) {
        var jsonPropertyNameAttr = property.GetCustomAttribute<JsonPropertyAttribute>();
        return jsonPropertyNameAttr?.PropertyName ?? property.Name;
    }
}

/// <summary>
///     Attribute to constrain a property to specific enum values in the JSON schema.
///     Usage: [EnumConstraint("Value1", "Value2", "Value3")]
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class EnumConstraintAttribute : Attribute {
    /// <summary>
    ///     Creates an enum constraint with the specified allowed values
    /// </summary>
    /// <param name="values">The allowed string values for this property</param>
    public EnumConstraintAttribute(params string[] values) => this.Values = values;

    public IEnumerable<string> Values { get; }
}