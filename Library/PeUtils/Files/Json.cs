using Json.Schema;
using Json.Schema.Generation;
using SysJson = System.Text.Json;
using SysJsonNodes = System.Text.Json.Nodes;

namespace PeUtils.Files;

public class Json<T> where T : class, new() {
    private readonly SysJson.JsonSerializerOptions _jsonOptions;
    private readonly JsonSchema _schema;
    public readonly string FilePath;

    public Json(string filePath) {
        FileUtils.ValidateFileNameAndExtension(filePath, "json");
        this.FilePath = filePath;
        this._jsonOptions =
            new SysJson.JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        this._schema = new JsonSchemaBuilder()
            .FromType<T>()
            .AdditionalProperties(false)
            .Build();
        this.SaveSchema();
        this.SaveJson();
    }

    /// <summary> Reads JSON settings from the specified file, validating against schema </summary>
    /// <returns>Deserialized settings object</returns>
    public T Read() {
        this.SaveJson();

        var jsonContent = File.ReadAllText(this.FilePath);
        var jsonNode = SysJsonNodes.JsonNode.Parse(jsonContent);

        var validationResults = this._schema.Evaluate(jsonNode);
        if (!validationResults.IsValid) {
            var errors = new List<string>();
            CollectValidationErrors(validationResults, errors);

            throw new JsonValidationException(errors);
        }

        var settings = SysJson.JsonSerializer.Deserialize<T>(jsonContent, this._jsonOptions);
        return settings ?? new T();
    }

    /// <summary> Writes settings object to JSON file after validation </summary>
    /// <param name="settings">Settings object to save</param>
    public void Write(T settings) {
        var jsonContent = SysJson.JsonSerializer.Serialize(settings, this._jsonOptions);
        var jsonNode = SysJsonNodes.JsonNode.Parse(jsonContent);

        // Validate before saving
        var validationResults = this._schema.Evaluate(jsonNode);
        if (!validationResults.IsValid) {
            var errors = new List<string>();
            CollectValidationErrors(validationResults, errors);
            throw new JsonValidationException(errors);
        }

        File.WriteAllText(this.FilePath, jsonContent);
    }

    private void SaveJson() {
        if (!File.Exists(this.FilePath)) {
            var defaultSettings = new T();
            this.Write(defaultSettings);
            throw new CrashProgramException(
                $"File {this.FilePath} did not exist. A default file was created, please review it and try again.");
        }
    }

    /// <summary> Saves the JSON schema to a .schema.json file </summary>
    private void SaveSchema() {
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory == null) return;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FilePath);
        var schemaPath = Path.Combine(directory, $"{fileNameWithoutExtension}.schema.json");
        var schemaJson = SysJson.JsonSerializer.Serialize(this._schema, this._jsonOptions);
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
                "Validation failed at root level. An additional, missing, or misspelled property likely exists. Check for schema mismatch or delete the it and try again.");
        }

        if (results.HasDetails) {
            foreach (var detail in results.Details)
                CollectValidationErrors(detail, errors);
        }

        if (!results.IsValid && errors.Count == 0 && !results.HasDetails) {
            errors.Add(
                $"Validation failed at '{results.InstanceLocation}' but no specific error was provided. Check for schema mismatch or delete the file and try again.");
        }
    }
}