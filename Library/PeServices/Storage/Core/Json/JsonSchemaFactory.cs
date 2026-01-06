using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using PeServices.Storage.Core.Json.SchemaProcessors;

namespace PeServices.Storage.Core.Json;

/// <summary>
///     Factory for creating JSON schemas with standardized processor configuration.
///     Consolidates schema generation logic shared between Json&lt;T&gt; and JsonWithExtends&lt;T&gt;.
/// </summary>
public static class JsonSchemaFactory {
    /// <summary>
    ///     Creates a JSON schema for type T with all standard processors registered.
    ///     Includes RevitTypeSchemaProcessor, OneOfSchemaProcessor, and SchemaExamplesProcessor.
    /// </summary>
    /// <returns>Generated JSON schema with all processors applied</returns>
    public static JsonSchema CreateSchema<T>(out SchemaExamplesProcessor examplesProcessor) {
        RevitTypeRegistry.Initialize();

        var settings = new NewtonsoftJsonSchemaGeneratorSettings {
            FlattenInheritanceHierarchy = true,
            AlwaysAllowAdditionalObjectProperties = false
        };

        // Add individual TypeMappers for each registered Revit type
        // Each mapper has the correct MappedType, preventing NJsonSchema from traversing
        foreach (var mapper in RevitTypeRegistry.CreateTypeMappers())
            settings.TypeMappers.Add(mapper);

        examplesProcessor = new SchemaExamplesProcessor();
        settings.SchemaProcessors.Add(new RevitTypeSchemaProcessor());
        settings.SchemaProcessors.Add(new OneOfSchemaProcessor());
        settings.SchemaProcessors.Add(examplesProcessor);

        return new JsonSchemaGenerator(settings).Generate(typeof(T));
    }

    /// <summary>
    ///     Writes the schema file to disk and injects $schema reference into JSON content.
    ///     Returns the modified JSON content string with $schema property added.
    /// </summary>
    /// <returns>Modified JSON content with $schema property</returns>
    public static string WriteAndInjectSchema(
        JsonSchema schema,
        string jsonContent,
        string targetFilePath
    ) {
        // Ensure directory exists
        var directory = Path.GetDirectoryName(targetFilePath);
        if (directory != null && !Directory.Exists(directory))
            _ = Directory.CreateDirectory(directory);

        // Write schema file
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetFilePath);
        var schemaPath = Path.Combine(directory, $"{fileNameWithoutExtension}.schema.json");
        File.WriteAllText(schemaPath, schema.ToJson());

        // Inject $schema reference into JSON content
        var jObject = JObject.Parse(jsonContent);
        jObject["$schema"] = Path.GetFileName(schemaPath);
        return JsonConvert.SerializeObject(jObject, Formatting.Indented);
    }
}