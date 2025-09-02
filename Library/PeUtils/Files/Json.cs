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
        this._jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        this._schema = new JsonSchemaBuilder()
            .FromType<T>()
            .AdditionalProperties(false)
            .Build();
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
            var errors = new List<string>();
            CollectValidationErrors(validationResults, errors);

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
            var errors = new List<string>();
            CollectValidationErrors(validationResults, errors);

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

    /// <summary> Recursively collects validation errors from evaluation results </summary>
    private static void CollectValidationErrors(EvaluationResults results, List<string> errors) {
        if (results.HasErrors && results?.Errors != null) {
            foreach (var error in results.Errors)
                errors.Add($"At '{results.InstanceLocation}': {error.Key} - {error.Value}");
        }

        if (!results.IsValid && !results.HasErrors && results.InstanceLocation.ToString() == "") {
            errors.Add(
                "Validation failed at root level. This likely indicates additional, missing, or misspelled properties. Refer to the schema to verify, or delete the settings json file to regenerate it on next launch.");
        }

        if (results.HasDetails) {
            foreach (var detail in results.Details)
                CollectValidationErrors(detail, errors);
        }

        if (!results.IsValid && errors.Count == 0 && !results.HasDetails) {
            errors.Add(
                $"Validation failed at '{results.InstanceLocation}' but no specific error was provided. Check for additional properties or schema mismatch.");
        }
    }
}