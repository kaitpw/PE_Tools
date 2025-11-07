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

namespace PeServices.Storage.Core;

public class Json<T> : JsonReadWriter<T> where T : class, new() {
    private readonly JsonSerializerSettings _deserialSettings = new() {
        Formatting = Formatting.Indented,
        Converters = new List<JsonConverter> { new StringEnumConverter(), new ForgeTypeIdConverter() },
        ContractResolver = new OrderedContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore
    };

    private readonly DateTime _instanceCreationTime;
    private readonly JsonSchema _schema;

    private readonly JsonSerializerSettings _serialSettings = new() {
        Formatting = Formatting.Indented,
        Converters = new List<JsonConverter> { new StringEnumConverter(), new ForgeTypeIdConverter() },
        ContractResolver = new RequiredAwareContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore

    };

    public Json(string filePath, bool throwIfDefaultCreated, bool saveSchema) {
        FileUtils.ValidateFileNameAndExtension(filePath, "json");
        this.FilePath = filePath;
        this._instanceCreationTime = DateTime.Now;
        var settings = new NewtonsoftJsonSchemaGeneratorSettings { FlattenInheritanceHierarchy = true };
        settings.SchemaProcessors.Add(new EnumConstraintSchemaProcessor());
        settings.SchemaProcessors.Add(new ForgeTypeIdSchemaProcessor());
        this._schema = new JsonSchemaGenerator(settings).Generate(typeof(T));

        _ = this.EnsureDirectoryExists();

        if (File.Exists(this.FilePath) && this.CurrJObject().HasValues) {
            // Always deserialize and re-serialize to sanitize the JSON file.
            var originalJson = this.CurrJObject();
            var sanitizedJsonText = this.Deserialize();
            this.WritePossiblyInvalid(sanitizedJsonText);
            var updatedJson = this.CurrJObject();

            var addedProps = JsonRecovery.GetAddedProperties(originalJson, updatedJson);
            var removedProps = JsonRecovery.GetRemovedProperties(originalJson, updatedJson);
            if (addedProps.Any() || removedProps.Any()) {
                var message = $"JSON file {this.FilePath} has been updated.";
                if (addedProps.Any()) message += $"\nAdded properties:\n\t-{string.Join("\n\t-", addedProps)}";
                if (removedProps.Any()) message += $"\nRemoved properties:\n\t-{string.Join("\n\t-", removedProps)}";
                message += "\nPlease review the settings before running again.";
                throw new CrashProgramException(message);
            } else {
                var valErrs = this._schema.Validate(this.CurrJObject());
                if (valErrs.Any())
                    throw new JsonValidationException(this.FilePath, valErrs);
                else return;
            }
        } else {
            this.WritePossiblyInvalid(new T());
            if (throwIfDefaultCreated) {
                throw new CrashProgramException(
                    $"File {this.FilePath} did not exist. A default file was created, please review it and try again.");
            }
        }

        if (saveSchema) this.WriteSchema();
    }

    public string FilePath { get; init; }


    /// <summary> Reads JSON object from the specified file, validating against schema </summary>
    /// <returns>Deserialized object</returns>
    public T Read() {
        var content = this.Deserialize();
        return content;
    }

    public void Write(T content) {
        _ = this.EnsureDirectoryExists();
        var jsonContent = this.Serialize(content);
        var validationErrs = this._schema.Validate(jsonContent).ToList();
        if (validationErrs.Any())
            throw new JsonValidationException(this.FilePath, validationErrs);
        File.WriteAllText(this.FilePath, jsonContent);
    }

    /// <summary>
    ///     Checks if the cached data is valid based on age and content.
    /// </summary>
    /// <param name="maxAgeMinutes">Maximum age in minutes before cache is considered invalid</param>
    /// <param name="contentValidator">
    ///     Optional function to validate the cached content like, for example, checking for an
    ///     empty cache
    /// </param>
    /// <returns>True if cache is valid and can be used</returns>
    public bool IsCacheValid(int maxAgeMinutes, Func<T, bool> contentValidator = null) {
        if (!File.Exists(this.FilePath)) return false;

        var fileLastWrite = File.GetLastWriteTime(this.FilePath);
        var cacheAge = DateTime.Now - fileLastWrite;

        // Check if cache is too old
        if (cacheAge.TotalMinutes > maxAgeMinutes) return false;

        // Check content validity if validator is provided
        if (contentValidator != null) {
            var content = this.Read();
            return contentValidator(content);
        }

        return true;
    }

    /// <summary> Ensures the directory for the file path exists </summary>
    private string EnsureDirectoryExists() {
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory != null && !Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);
        return directory;
    }

    public JObject CurrJObject() => JObject.Parse(File.ReadAllText(this.FilePath));
    public T Deserialize() {
        var text = File.ReadAllText(this.FilePath);
        return JsonConvert.DeserializeObject<T>(text, this._deserialSettings);
    }
    public string Serialize(T content) => JsonConvert.SerializeObject(content, this._serialSettings);

    public void WritePossiblyInvalid(T content) {
        _ = this.EnsureDirectoryExists();
        var jsonContent = this.Serialize(content);
        File.WriteAllText(this.FilePath, jsonContent);
    }

    /// <summary> Saves the JSON schema to a .schema.json file </summary>
    private void WriteSchema() {
        _ = this.EnsureDirectoryExists();
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory == null) return;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FilePath);
        var schemaPath = Path.Combine(directory, $"{fileNameWithoutExtension}.schema.json");
        var schemaJson = this._schema.ToJson();
        File.WriteAllText(schemaPath, schemaJson);
    }
}

public static class ValidationErrorCollectionExtensions {
    public static bool HasAdditionalPropertiesError(this ICollection<ValidationError> errors) =>
        errors.Any(e => e.Kind == ValidationErrorKind.NoAdditionalPropertiesAllowed);

    /// <summary> Recursively checks if any validation error is a PropertyRequired error </summary>
    public static bool HasPropertyRequiredError(this ICollection<ValidationError> errors) {
        foreach (var error in errors) {
            if (error.Kind == ValidationErrorKind.PropertyRequired) return true;

            // Check nested errors in ChildSchemaValidationError
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
    /// <summary> Gets all property paths from a JSON object </summary>
    private static List<string> GetAllPropertyPaths(JObject obj, string prefix = "") {
        var paths = new List<string>();
        foreach (var prop in obj.Properties()) {
            var path = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            paths.Add(path);
            if (prop.Value is JObject nestedObj) paths.AddRange(GetAllPropertyPaths(nestedObj, path));
        }

        return paths;
    }

    /// <summary> Gets properties that were added (exist in updated but not in original) </summary>
    public static List<string> GetAddedProperties(JObject original, JObject updated) {
        var originalPaths = GetAllPropertyPaths(original);
        var updatedPaths = GetAllPropertyPaths(updated);
        return updatedPaths.Except(originalPaths).ToList();
    }

    /// <summary> Gets properties that were removed (exist in original but not in updated) </summary>
    public static List<string> GetRemovedProperties(JObject original, JObject updated) {
        var originalPaths = GetAllPropertyPaths(original);
        var updatedPaths = GetAllPropertyPaths(updated);
        return originalPaths.Except(updatedPaths).ToList();
    }
}