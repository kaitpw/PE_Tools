using Json.Schema;
using Json.Schema.Generation;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PeUtils.Files;

public class Json<T> where T : class, new() {
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSchema _schema;
    public readonly string FilePath;

    public Json(string filePath) {
        FileUtils.ValidateFileNameAndExtension(filePath, "json");
        this.FilePath = filePath;
        this._jsonOptions = new JsonSerializerOptions {
            WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        this._schema = new JsonSchemaBuilder().FromType<T>().Build();
        this.SaveSchema();
    }

    /// <summary> Reads JSON settings from the specified file, validating against schema </summary>
    /// <returns>Deserialized settings object</returns>
    public T Read() {
        if (!File.Exists(this.FilePath)) {
            var defaultSettings = new T();
            this.Write(defaultSettings);
            return defaultSettings;
        }

        var jsonContent = File.ReadAllText(this.FilePath);
        var jsonNode = JsonNode.Parse(jsonContent);

        var validationResults = this._schema.Evaluate(jsonNode);
        if (!validationResults.IsValid) {
            var errors = validationResults.Details
                .Where(d => d.HasErrors)
                .SelectMany(d => d.Errors.Select(e =>
                    $"At '{d.InstanceLocation}': {e.Key} - {e.Value}"))
                .ToList();

            throw new JsonValidationException(errors);
        }

        var settings = JsonSerializer.Deserialize<T>(jsonContent, this._jsonOptions);
        return settings ?? new T();
    }

    /// <summary> Writes settings object to JSON file after validation </summary>
    /// <param name="settings">Settings object to save</param>
    public void Write(T settings) {
        var jsonContent = JsonSerializer.Serialize(settings, this._jsonOptions);
        var jsonNode = JsonNode.Parse(jsonContent);

        // Validate before saving
        var validationResults = this._schema.Evaluate(jsonNode);
        if (!validationResults.IsValid) {
            var errors = validationResults.Details
                .Where(d => d.HasErrors)
                .SelectMany(d => d.Errors.Select(e =>
                    $"At '{d.InstanceLocation}': {e.Key} - {e.Value}"))
                .ToList();

            throw new JsonValidationException(errors);
        }

        ;
        File.WriteAllText(this.FilePath, jsonContent);
    }

    /// <summary> Saves the JSON schema to a .schema.json file </summary>
    private void SaveSchema() {
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory == null) return;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FilePath);
        var schemaPath = Path.Combine(directory, $"{fileNameWithoutExtension}.schema.json");
        var schemaJson = JsonSerializer.Serialize(this._schema, this._jsonOptions);
        File.WriteAllText(schemaPath, schemaJson);
    }
}