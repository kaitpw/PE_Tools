using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NJsonSchema.Validation;
using PeServices.Storage.Core.Json.ContractResolvers;
using PeServices.Storage.Core.Json.Converters;
using PeServices.Storage.Core.Json.SchemaProcessors;
using PeUtils.Files;
using System.Text.RegularExpressions;

namespace PeServices.Storage.Core;

/// <summary>
///     Core JSON file handler with schema validation. Provides explicit methods for different
///     read/write behaviors - consumers choose the appropriate method for their use case.
/// </summary>
public class Json<T> where T : class, new() {
    private readonly JsonSerializerSettings _deserialSettings = new() {
        Formatting = Formatting.Indented,
        Converters = new List<JsonConverter> { new StringEnumConverter(), new ForgeTypeIdConverter() },
        ContractResolver = new OrderedContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly JsonSchema _schema;

    private readonly JsonSerializerSettings _serialSettings = new() {
        Formatting = Formatting.Indented,
        Converters = new List<JsonConverter> { new StringEnumConverter(), new ForgeTypeIdConverter() },
        ContractResolver = new RequiredAwareContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    public Json(string filePath) {
        FileUtils.ValidateFileNameAndExtension(filePath, "json");
        this.FilePath = filePath;
        _ = this.EnsureDirectoryExists();

        var settings = new NewtonsoftJsonSchemaGeneratorSettings {
            FlattenInheritanceHierarchy = true, AlwaysAllowAdditionalObjectProperties = false
        };

        var examplesProcessor = new SchemaExamplesProcessor();
        settings.SchemaProcessors.Add(new EnumConstraintSchemaProcessor());
        settings.SchemaProcessors.Add(new ForgeTypeIdSchemaProcessor());
        settings.SchemaProcessors.Add(examplesProcessor);
        this._schema = new JsonSchemaGenerator(settings).Generate(typeof(T));

        // Let the examples processor finalize (add $defs if consolidating)
        examplesProcessor.Finalize(this._schema);

        SchemaMetadataProcessor.AllowSchemaProperty(this._schema);
    }

    public string FilePath { get; }
    public bool FileExists => File.Exists(this.FilePath);

    // ============================================================
    // READ METHODS
    // ============================================================

    /// <summary>
    ///     Simple read with validation. File must exist.
    /// </summary>
    public T Read() {
        this.Validate(this.ReadJObject());
        return this.Deserialize();
    }

    /// <summary>
    ///     Read with sanitization: deserializes then re-serializes to update schema,
    ///     detects property changes, and validates. Throws if properties were added/removed.
    /// </summary>
    public T ReadAndSanitize() {
        var originalJson = this.ReadJObject();

        // Attempt deserialization with type migrations if needed
        T content;
        var appliedMigrations = new List<string>();
        try {
            content = this.Deserialize();
        } catch (JsonSerializationException ex) {
            // Try applying type migrations to fix deserialization errors
            var migratedJson = JsonTypeMigrations.TryApplyMigrations(originalJson, ex, out appliedMigrations);
            if (migratedJson != null && appliedMigrations.Any()) {
                // Write migrated JSON to disk (one-time fix)
                var migratedText = JsonConvert.SerializeObject(migratedJson, Formatting.Indented);
                File.WriteAllText(this.FilePath, migratedText);
                content = this.Deserialize();
            } else
                throw; // No migrations applied, re-throw original exception
        }

        // Re-serialize to normalize the JSON (applies current schema structure)
        this.WriteRaw(content, true);
        var updatedJson = this.ReadJObject();

        // Detect schema drift
        var addedProps = JsonRecovery.GetAddedProperties(originalJson, updatedJson);
        var removedProps = JsonRecovery.GetRemovedProperties(originalJson, updatedJson);
        if (addedProps.Any() || removedProps.Any())
            throw new JsonSanitizationException(this.FilePath, addedProps, removedProps, appliedMigrations);

        this.Validate(updatedJson);
        return content;
    }

    /// <summary>
    ///     Read with automatic default creation. If file doesn't exist, creates a default
    ///     instance, writes it, and returns it. Never throws for missing files.
    /// </summary>
    public T ReadOrCreate() {
        if (!this.FileExists) {
            var defaultContent = new T();
            this.WriteRaw(defaultContent, true);
            return defaultContent;
        }

        return this.Read();
    }

    // ============================================================
    // WRITE METHODS
    // ============================================================

    /// <summary>
    ///     Write with validation. Throws if content doesn't match schema.
    /// </summary>
    public void Write(T content) => this.Write(content, false);

    /// <summary>
    ///     Write with validation, optionally injecting schema reference.
    /// </summary>
    public void Write(T content, bool injectSchemaRef) {
        var jsonContent = this.Serialize(content);
        this.Validate(JObject.Parse(jsonContent));
        this.WriteRaw(content, injectSchemaRef);
    }

    /// <summary>
    ///     Write without validation. Use for writing defaults or known-good content.
    /// </summary>
    public void WriteUnvalidated(T content) => this.WriteUnvalidated(content, false);

    /// <summary>
    ///     Write without validation, optionally injecting schema reference.
    /// </summary>
    public void WriteUnvalidated(T content, bool injectSchemaRef) => this.WriteRaw(content, injectSchemaRef);

    /// <summary>
    ///     Writes the JSON schema file (.schema.json) for IDE IntelliSense support.
    /// </summary>
    public void WriteSchema() {
        _ = this.EnsureDirectoryExists();
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory == null) return;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FilePath);
        var schemaPath = Path.Combine(directory, $"{fileNameWithoutExtension}.schema.json");
        var schemaJson = this._schema.ToJson();
        File.WriteAllText(schemaPath, schemaJson);
    }

    // ============================================================
    // UTILITY METHODS
    // ============================================================

    /// <summary>
    ///     Checks if the cached data is valid based on age and content.
    /// </summary>
    public bool IsCacheValid(int maxAgeMinutes, Func<T, bool> contentValidator = null) {
        if (!this.FileExists) return false;

        var fileLastWrite = File.GetLastWriteTime(this.FilePath);
        var cacheAge = DateTime.Now - fileLastWrite;

        if (cacheAge.TotalMinutes > maxAgeMinutes) return false;

        if (contentValidator != null) {
            var content = this.Read();
            return contentValidator(content);
        }

        return true;
    }

    // ============================================================
    // INTERNAL BUILDING BLOCKS (DRY - single source of truth)
    // ============================================================

    /// <summary> Core validation - single source of truth for schema validation </summary>
    private void Validate(JObject jObject) {
        var errors = this._schema.Validate(jObject).ToList();
        if (errors.Any())
            throw new JsonValidationException(this.FilePath, errors);
    }

    /// <summary> Core deserialization - single source of truth </summary>
    private T Deserialize() {
        var text = File.ReadAllText(this.FilePath);
        return JsonConvert.DeserializeObject<T>(text, this._deserialSettings);
    }

    /// <summary> Core serialization - single source of truth </summary>
    private string Serialize(T content) => JsonConvert.SerializeObject(content, this._serialSettings);

    /// <summary> Core write operation - all writes flow through here </summary>
    private void WriteRaw(T content, bool injectSchemaRef) {
        _ = this.EnsureDirectoryExists();
        var jsonContent = this.Serialize(content);

        if (injectSchemaRef)
            jsonContent = this.InjectSchemaReference(jsonContent);

        File.WriteAllText(this.FilePath, jsonContent);
    }

    /// <summary> Read file as JObject for validation/comparison </summary>
    private JObject ReadJObject() => JObject.Parse(File.ReadAllText(this.FilePath));

    /// <summary> Ensures the directory for the file path exists </summary>
    private string EnsureDirectoryExists() {
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory != null && !Directory.Exists(directory))
            _ = Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>
    ///     Injects the $schema property for IDE IntelliSense support.
    /// </summary>
    private string InjectSchemaReference(string jsonContent) {
        var jObject = JObject.Parse(jsonContent);
        var schemaFileName = $"./{Path.GetFileNameWithoutExtension(this.FilePath)}.schema.json";
        jObject["$schema"] = schemaFileName;
        return JsonConvert.SerializeObject(jObject, Formatting.Indented);
    }
}

public static class ValidationErrorCollectionExtensions {
    public static bool HasAdditionalPropertiesError(this ICollection<ValidationError> errors) =>
        errors.Any(e => e.Kind == ValidationErrorKind.NoAdditionalPropertiesAllowed);

    /// <summary> Recursively checks if any validation error is a PropertyRequired error </summary>
    public static bool HasPropertyRequiredError(this ICollection<ValidationError> errors) {
        foreach (var error in errors) {
            if (error.Kind == ValidationErrorKind.PropertyRequired) return true;

            if (error is ChildSchemaValidationError childError) {
                foreach (var nestedErrors in childError.Errors.Values) {
                    if (HasPropertyRequiredError(nestedErrors))
                        return true;
                }
            }
        }

        return false;
    }
}

/// <summary> Handles JSON recovery operations for schema validation errors </summary>
file static class JsonRecovery {
    private static readonly HashSet<string> IgnoredProperties = ["$schema", "$extends"];

    private static List<string> GetAllPropertyPaths(JObject obj, string prefix = "") {
        var paths = new List<string>();
        foreach (var prop in obj.Properties()) {
            if (string.IsNullOrEmpty(prefix) && IgnoredProperties.Contains(prop.Name))
                continue;

            var path = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            paths.Add(path);
            if (prop.Value is JObject nestedObj) paths.AddRange(GetAllPropertyPaths(nestedObj, path));
        }

        return paths;
    }

    public static List<string> GetAddedProperties(JObject original, JObject updated) {
        var originalPaths = GetAllPropertyPaths(original);
        var updatedPaths = GetAllPropertyPaths(updated);
        return updatedPaths.Except(originalPaths).ToList();
    }

    public static List<string> GetRemovedProperties(JObject original, JObject updated) {
        var originalPaths = GetAllPropertyPaths(original);
        var updatedPaths = GetAllPropertyPaths(updated);
        return originalPaths.Except(updatedPaths).ToList();
    }
}

/// <summary>
///     Handles automatic type migrations for common JSON schema evolution patterns.
///     Migrations are applied transparently during ReadAndSanitize() and persisted to disk.
/// </summary>
file static class JsonTypeMigrations {
    /// <summary>
    ///     Attempts to apply type migrations to fix deserialization errors.
    ///     Returns migrated JObject if successful, null otherwise.
    /// </summary>
    public static JObject TryApplyMigrations(
        JObject json,
        JsonSerializationException exception,
        out List<string> appliedMigrations
    ) {
        appliedMigrations = new List<string>();

        // Extract property path and type mismatch from exception message
        // Example: "Error converting value "PE_P_LoadCalc_MinPipeSize" to type 'System.Collections.Generic.List`1[System.String]'. Path 'AddAndMapSharedParams.MappingData[0].CurrNames'"
        var exceptionMsg = exception.Message;
        var innerMsg = exception.InnerException?.Message ?? "";

        // Try to extract path using regex pattern
        var pathMatch = Regex.Match(exceptionMsg, @"Path '([^']+)'");
        if (!pathMatch.Success) return null;

        var propertyPath = pathMatch.Groups[1].Value;

        // Determine migration type from exception message
        var migratedJson = (JObject)json.DeepClone();
        var migrationApplied = false;

        // Migration 1: string → List<string>
        if (innerMsg.Contains("could not cast or convert from System.String to System.Collections.Generic.List") ||
            exceptionMsg.Contains("to type 'System.Collections.Generic.List`1[System.String]'"))
            migrationApplied = ApplyStringToListMigration(migratedJson, propertyPath, appliedMigrations);
        // Migration 2: number → string (future use)
        else if (innerMsg.Contains("could not convert from") && innerMsg.Contains("to System.String"))
            migrationApplied = ApplyNumberToStringMigration(migratedJson, propertyPath, appliedMigrations);

        return migrationApplied ? migratedJson : null;
    }

    /// <summary>
    ///     Migrates a string value to a List&lt;string&gt; by wrapping it in an array.
    ///     Handles both simple properties and array element properties.
    /// </summary>
    private static bool ApplyStringToListMigration(JObject json, string path, List<string> appliedMigrations) {
        try {
            // Parse path like "AddAndMapSharedParams.MappingData[0].CurrNames"
            var token = json.SelectToken(path);
            if (token == null || token.Type != JTokenType.String) return false;

            var stringValue = token.Value<string>();
            var arrayValue = new JArray(stringValue);

            // Replace the token in its parent
            if (token.Parent is JProperty property) {
                property.Value = arrayValue;
                appliedMigrations.Add(
                    $"Migrated '{path}' from string to array: \"{stringValue}\" → [\"{stringValue}\"]");
                return true;
            }

            return false;
        } catch {
            return false;
        }
    }

    /// <summary>
    ///     Migrates a number value to a string (for future use).
    /// </summary>
    private static bool ApplyNumberToStringMigration(JObject json, string path, List<string> appliedMigrations) {
        try {
            var token = json.SelectToken(path);
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return false;

            var numValue = token.ToString();
            var stringValue = new JValue(numValue);

            if (token.Parent is JProperty property) {
                property.Value = stringValue;
                appliedMigrations.Add($"Migrated '{path}' from number to string: {numValue} → \"{numValue}\"");
                return true;
            }

            return false;
        } catch {
            return false;
        }
    }
}