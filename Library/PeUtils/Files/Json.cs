using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using NJsonSchema.Validation;

namespace PeUtils.Files;

public class Json<T> where T : class, new() {
    private readonly DateTime _instanceCreationTime;
    private readonly JsonSchema _schema;
    private readonly JsonSerializerSettings _serializerSettings;
    public readonly string FilePath;

    public Json(string filePath, bool throwIfNotExists) {
        FileUtils.ValidateFileNameAndExtension(filePath, "json");
        this.FilePath = filePath;
        this._instanceCreationTime = DateTime.Now;
        this._serializerSettings = new JsonSerializerSettings {
            Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore
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

        this.SaveSchema();
        var fileDidntExist = !File.Exists(this.FilePath);
        this.SaveJson(); // Always create the file
        if (throwIfNotExists && fileDidntExist) {
            throw new CrashProgramException(
                $"File {this.FilePath} did not exist. A default file was created, please review it and try again.");
        }
    }

    /// <summary> Recursively checks if any validation error is a PropertyRequired error </summary>
    private static bool HasPropertyRequiredError(ICollection<ValidationError> errors) {
        foreach (var error in errors) {
            if (error.Kind == ValidationErrorKind.PropertyRequired) return true;

            // Check nested errors in ChildSchemaValidationError
            if (error is ChildSchemaValidationError childError) {
                foreach (var nestedErrors in childError.Errors.Values)
                    if (HasPropertyRequiredError(nestedErrors))
                        return true;
            }
        }

        return false;
    }

    /// <summary> Reads JSON object from the specified file, validating against schema </summary>
    /// <returns>Deserialized object</returns>
    /// <exception cref="System.IO.FileNotFoundException">Thrown when the file doesn't exist</exception>
    public T Read() {
        if (!File.Exists(this.FilePath))
            throw new FileNotFoundException($"JSON file not found: {this.FilePath}");

        var jsonContent = File.ReadAllText(this.FilePath);
        var validationErrors = this._schema.Validate(jsonContent);
        if (validationErrors.Any()) {
            var hasPropertyRequiredErrors = HasPropertyRequiredError(validationErrors);

            if (hasPropertyRequiredErrors) {
                try {
                    var partialContent = JsonConvert.DeserializeObject<T>(jsonContent, this._serializerSettings);
                    this.Write(partialContent ?? new T(), true);
                    throw new CrashProgramException(
                        $"JSON file {this.FilePath} was missing required properties and has been updated with defaults. Please review and configure the new settings before running again.");
                } catch (CrashProgramException) {
                    throw;
                } catch {
                    // If recovery fails, fall through to throw the original validation error
                }
            }

            var errors = validationErrors.Select(e => $"At '{e.Path}': {e.Kind} - {e}").ToList();
            throw new JsonValidationException(this.FilePath, errors);
        }

        var content = JsonConvert.DeserializeObject<T>(jsonContent, this._serializerSettings);
        return content ?? new T();
    }


    /// <summary> Writes object to JSON file after validation </summary>
    /// <param name="content">Object to save</param>
    /// <param name="skipValidation">Skip validation when writing (used for recovery scenarios)</param>
    public void Write(T content, bool skipValidation = false) {
        var jsonContent = JsonConvert.SerializeObject(content, this._serializerSettings);

        if (!skipValidation) {
            var validationErrors = this._schema.Validate(jsonContent);
            if (validationErrors.Any()) {
                var errors = validationErrors.Select(e => $"At '{e.Path}': {e.Kind} - {e}").ToList();
                throw new JsonValidationException(this.FilePath, errors);
            }
        }

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

    private void SaveJson() {
        if (!File.Exists(this.FilePath)) {
            var defaultContent = new T();
            this.Write(defaultContent);
        }
    }

    /// <summary> Saves the JSON schema to a .schema.json file </summary>
    private void SaveSchema() {
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory == null) return;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FilePath);
        var schemaPath = Path.Combine(directory, $"{fileNameWithoutExtension}.schema.json");
        var schemaJson = this._schema.ToJson();
        File.WriteAllText(schemaPath, schemaJson);
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