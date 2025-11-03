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

namespace PeServices.Storage.Core;

/// <summary> Handles JSON recovery operations for schema validation errors </summary>
file static class JsonRecovery {
    /// <summary> Recursively checks if any validation error is a PropertyRequired error </summary>
    public static bool HasPropertyRequiredError(ICollection<ValidationError> errors) {
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
    private static List<string> GetAddedProperties(JObject original, JObject updated) {
        var originalPaths = GetAllPropertyPaths(original);
        var updatedPaths = GetAllPropertyPaths(updated);
        return updatedPaths.Except(originalPaths).ToList();
    }

    /// <summary> Gets properties that were removed (exist in original but not in updated) </summary>
    private static List<string> GetRemovedProperties(JObject original, JObject updated) {
        var originalPaths = GetAllPropertyPaths(original);
        var updatedPaths = GetAllPropertyPaths(updated);
        return originalPaths.Except(updatedPaths).ToList();
    }

    /// <summary>
    ///     Attempts to recover from JSON validation errors by fixing the file and throwing a CrashProgramException
    /// </summary>
    public static CrashProgramException AttemptRecovery<T>(
        string filePath,
        Func<string> fileText,
        JsonSerializerSettings serializerSettings) where T : class, new() {
        var originalJson = JObject.Parse(fileText());
        var partialContent = JsonConvert.DeserializeObject<T>(fileText(), serializerSettings);

        // Ensure directory exists before writing
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, JsonConvert.SerializeObject(partialContent ?? new T(), serializerSettings));

        var updatedJson = JObject.Parse(fileText());
        var addedProps = GetAddedProperties(originalJson, updatedJson);
        var removedProps = GetRemovedProperties(originalJson, updatedJson);

        var message = $"JSON file {filePath} had schema validation errors and has been updated.";
        if (addedProps.Any()) message += $"\nAdded properties: {string.Join("\n\t-", addedProps)}";
        if (removedProps.Any()) message += $"\nRemoved properties: {string.Join("\n\t-", removedProps)}";
        message += "\nPlease review the settings before running again.";

        return new CrashProgramException(message);
    }
}

/// <summary> Contract resolver that orders properties by declaration order, respecting inheritance hierarchy </summary>
file class OrderedContractResolver : DefaultContractResolver {
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
        var properties = base.CreateProperties(type, memberSerialization);

        // Build inheritance chain from base to derived
        var typeHierarchy = new List<Type>();
        var currentType = type;
        while (currentType != null && currentType != typeof(object)) {
            typeHierarchy.Insert(0, currentType);
            currentType = currentType.BaseType;
        }

        // Create ordered list: base class properties first, then derived class properties
        var orderedProperties = new List<JsonProperty>();
        foreach (var t in typeHierarchy) {
            var declaredProps = t.GetProperties(BindingFlags.Public |
                                                BindingFlags.Instance |
                                                BindingFlags.DeclaredOnly);

            foreach (var declaredProp in declaredProps) {
                var jsonProp = properties.FirstOrDefault(p => p.UnderlyingName == declaredProp.Name);
                if (jsonProp != null && !orderedProperties.Contains(jsonProp)) orderedProperties.Add(jsonProp);
            }
        }

        return orderedProperties;
    }
}

public class Json<T> : JsonReadWriter<T> where T : class, new() {
    private readonly DateTime _instanceCreationTime;
    private readonly JsonSchema _schema;
    private readonly JsonSerializerSettings _serializerSettings;
    public string FilePath { get; init; }
    public string FileText() => File.ReadAllText(this.FilePath);

    public Json(string filePath, bool throwIfDefaultCreated, bool saveSchema) {
        FileUtils.ValidateFileNameAndExtension(filePath, "json");
        this.FilePath = filePath;
        this._instanceCreationTime = DateTime.Now;
        this._serializerSettings = new JsonSerializerSettings {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            ContractResolver = new OrderedContractResolver()
        };

        var schemaSettings = new NewtonsoftJsonSchemaGeneratorSettings {
            AllowReferencesWithProperties = false, // default, keep for explicatory purposes
            AlwaysAllowAdditionalObjectProperties = false, // default, keep for explicatory purposes
            FlattenInheritanceHierarchy = true
        };

        // Add custom schema processor for enum constraints
        schemaSettings.SchemaProcessors.Add(new EnumConstraintSchemaProcessor());

        var generator = new JsonSchemaGenerator(schemaSettings);
        this._schema = generator.Generate(typeof(T));

        this.EnsureDirectoryExists();

        if (File.Exists(this.FilePath) && this.FileText().Length > 0) {
            var (validationErrs, err) = this.TryValidateAndRecover();
            if (err != null) throw err;

            if (saveSchema) this.WriteSchema();
            if (validationErrs.Any())
                throw new JsonValidationException(this.FilePath, validationErrs);
        } else {
            this.WritePossiblyInvalid(new T());
            if (saveSchema) this.WriteSchema();
            if (throwIfDefaultCreated) {
                throw new CrashProgramException(
                    $"File {this.FilePath} did not exist. A default file was created, please review it and try again.");
            }
        }
    }

    /// <summary> Reads JSON object from the specified file, validating against schema </summary>
    /// <returns>Deserialized object</returns>
    public T Read() {
        this.EnsureDirectoryExists();
        var (validationErrs, err) = this.TryValidateAndRecover();
        if (err != null) throw err;
        if (validationErrs.Any())
            throw new JsonValidationException(this.FilePath, validationErrs);
        var content = JsonConvert.DeserializeObject<T>(this.FileText(), this._serializerSettings);
        return content ?? new T();
    }

    public void Write(T content) {
        this.EnsureDirectoryExists();
        var jsonContent = JsonConvert.SerializeObject(content, this._serializerSettings);
        var validationErrs = this._schema.Validate(jsonContent).ToList();
        if (validationErrs.Any())
            throw new JsonValidationException(this.FilePath, validationErrs);
        File.WriteAllText(this.FilePath, jsonContent);
    }

    public void WritePossiblyInvalid(T content) {
        this.EnsureDirectoryExists();
        var jsonContent = JsonConvert.SerializeObject(content, this._serializerSettings);
        File.WriteAllText(this.FilePath, jsonContent);
    }

    /// <summary> Saves the JSON schema to a .schema.json file </summary>
    private void WriteSchema() {
        this.EnsureDirectoryExists();
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory == null) return;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FilePath);
        var schemaPath = Path.Combine(directory, $"{fileNameWithoutExtension}.schema.json");
        var schemaJson = this._schema.ToJson();
        File.WriteAllText(schemaPath, schemaJson);
    }

    /// <summary> Ensures the directory for the file path exists </summary>
    private void EnsureDirectoryExists() {
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory != null && !Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);
    }

    /// <summary>
    ///     Validates JSON content and attempts recovery if needed. Throws exceptions on validation failure.
    ///     Used for reading and writing where recovery is acceptable.
    /// </summary>
    /// <returns>Returns a CrashProgramException. If recovery is not possible it returns validation errors.</returns>
    private Result<IEnumerable<ValidationError>> TryValidateAndRecover() {
        var validationErrors = this._schema.Validate(this.FileText()).ToList();
        if (!validationErrors.Any()) return validationErrors;

        var hasPropertyRequiredErrors = JsonRecovery.HasPropertyRequiredError(validationErrors);
        var hasAdditionalPropertiesErrors =
            validationErrors.Any(e => e.Kind == ValidationErrorKind.NoAdditionalPropertiesAllowed);

        if (hasPropertyRequiredErrors || hasAdditionalPropertiesErrors)
            return JsonRecovery.AttemptRecovery<T>(this.FilePath, this.FileText, this._serializerSettings);

        return validationErrors;
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
}

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