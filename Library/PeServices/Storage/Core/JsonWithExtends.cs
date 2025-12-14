using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.Generation;
using NJsonSchema.NewtonsoftJson.Generation;
using PeServices.Storage.Core.Json;
using PeServices.Storage.Core.Json.ContractResolvers;
using PeServices.Storage.Core.Json.Converters;
using PeServices.Storage.Core.Json.SchemaProcessors;
using PeUtils.Files;

namespace PeServices.Storage.Core;

/// <summary>
///     JSON reader that supports profile inheritance via the <c>$extends</c> property.
///     Child profiles can be sparse, containing only overrides of base profile values.
/// </summary>
/// <remarks>
///     <para>Inheritance behavior:</para>
///     <list type="bullet">
///         <item>If no <c>$extends</c> property: behaves like standard <see cref="Json{T}" /></item>
///         <item>If <c>$extends</c> exists: loads base profile, merges child on top, validates merged result</item>
///         <item>Base profiles get full recovery (sanitize/update); child profiles stay sparse</item>
///         <item>Supports multi-level inheritance (A extends B extends C)</item>
///     </list>
/// </remarks>
/// <typeparam name="T">The type to deserialize to</typeparam>
public class JsonWithExtends<T> : JsonReader<T> where T : class, new() {
    private const string ExtendsProperty = "$extends";

    private readonly string _directoryPath;

    private readonly JsonSerializerSettings _deserialSettings = new() {
        Formatting = Formatting.Indented,
        Converters = new List<JsonConverter> { new StringEnumConverter(), new ForgeTypeIdConverter() },
        ContractResolver = new OrderedContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    private readonly JsonSchema _schema;
    private readonly bool _throwIfDefaultCreated;
    private readonly bool _saveSchema;

    public JsonWithExtends(string directoryPath, string filename, bool throwIfDefaultCreated, bool saveSchema) {
        this._directoryPath = directoryPath;
        this._throwIfDefaultCreated = throwIfDefaultCreated;
        this._saveSchema = saveSchema;
        this.FilePath = Path.Combine(directoryPath, filename);

        FileUtils.ValidateFileNameAndExtension(this.FilePath, "json");

        var schemaSettings = new NewtonsoftJsonSchemaGeneratorSettings { FlattenInheritanceHierarchy = true };
        schemaSettings.SchemaProcessors.Add(new EnumConstraintSchemaProcessor());
        schemaSettings.SchemaProcessors.Add(new ForgeTypeIdSchemaProcessor());
        this._schema = new JsonSchemaGenerator(schemaSettings).Generate(typeof(T));
    }

    public string FilePath { get; }

    /// <summary>
    ///     Reads the profile, resolving inheritance if <c>$extends</c> is present.
    /// </summary>
    public T Read() {
        if (!File.Exists(this.FilePath)) {
            // No file - delegate to standard Json<T> which handles default creation
            return new Json<T>(this.FilePath, this._throwIfDefaultCreated, this._saveSchema).Read();
        }

        var childJObject = JObject.Parse(File.ReadAllText(this.FilePath));

        // No extends? Use standard Json<T> with full recovery
        if (!childJObject.TryGetValue(ExtendsProperty, out var extendsToken)) {
            return new Json<T>(this.FilePath, this._throwIfDefaultCreated, this._saveSchema).Read();
        }

        // Validate $extends value
        if (extendsToken.Type != JTokenType.String || string.IsNullOrWhiteSpace(extendsToken.Value<string>())) {
            throw JsonExtendsException.InvalidExtendsValue(this.FilePath, extendsToken.Type.ToString());
        }

        var extendsName = extendsToken.Value<string>()!;

        // Resolve inheritance chain and merge
        var inheritanceChain = new List<string> { Path.GetFileNameWithoutExtension(this.FilePath) };
        var merged = this.ResolveInheritance(this.FilePath, childJObject, extendsName, inheritanceChain);

        // Validate merged result
        var validationErrors = this._schema.Validate(merged).ToList();
        if (validationErrors.Any()) {
            var errorMessages = string.Join("\n  - ", validationErrors.Select(e => $"{e.Path}: {e.Kind}"));
            throw JsonExtendsException.MergedValidationFailed(
                this.FilePath,
                this.GetBasePath(extendsName),
                errorMessages
            );
        }

        // Write schema for the profile (helps with IDE autocomplete)
        if (this._saveSchema) {
            this.WriteSchema();
        }

        // Deserialize merged result
        return JsonConvert.DeserializeObject<T>(merged.ToString(), this._deserialSettings)!;
    }

    /// <summary>
    ///     Recursively resolves the inheritance chain, loading and merging base profiles.
    /// </summary>
    private JObject ResolveInheritance(
        string childPath,
        JObject childJObject,
        string extendsName,
        List<string> inheritanceChain
    ) {
        var basePath = this.GetBasePath(extendsName);

        // Check for circular inheritance
        if (inheritanceChain.Contains(extendsName)) {
            inheritanceChain.Add(extendsName); // Add again to show the cycle
            throw JsonExtendsException.CircularInheritance(childPath, inheritanceChain);
        }

        // Check base exists
        if (!File.Exists(basePath)) {
            throw JsonExtendsException.BaseNotFound(childPath, extendsName, basePath);
        }

        inheritanceChain.Add(extendsName);

        // Load base profile
        JObject baseJObject;
        try {
            var baseContent = File.ReadAllText(basePath);
            baseJObject = JObject.Parse(baseContent);
        } catch (Exception ex) {
            throw JsonExtendsException.BaseValidationFailed(childPath, basePath, ex);
        }

        // Check if base also extends something (multi-level inheritance)
        if (baseJObject.TryGetValue(ExtendsProperty, out var baseExtendsToken)) {
            if (baseExtendsToken.Type != JTokenType.String ||
                string.IsNullOrWhiteSpace(baseExtendsToken.Value<string>())) {
                throw JsonExtendsException.InvalidExtendsValue(basePath, baseExtendsToken.Type.ToString());
            }

            var baseExtendsName = baseExtendsToken.Value<string>()!;

            // Recursively resolve base's inheritance first
            baseJObject = this.ResolveInheritance(basePath, baseJObject, baseExtendsName, inheritanceChain);
        } else {
            // Base has no extends - run it through standard Json<T> for recovery/validation
            try {
                // This triggers recovery and validation on the base file
                _ = new Json<T>(basePath, this._throwIfDefaultCreated, this._saveSchema).Read();
                // Re-read the possibly-updated base file
                baseJObject = JObject.Parse(File.ReadAllText(basePath));
            } catch (Exception ex) {
                throw JsonExtendsException.BaseValidationFailed(childPath, basePath, ex);
            }
        }

        // Remove $extends from child before merging (it's metadata, not data)
        var childForMerge = (JObject)childJObject.DeepClone();
        _ = childForMerge.Remove(ExtendsProperty);
        // if (!removalSuccess) throw TODO: idk how to handle an unsuccessful removal

        // Merge child onto base
        return JsonMerge.DeepMerge(baseJObject, childForMerge);
    }

    private string GetBasePath(string extendsName) {
        var baseName = extendsName.EndsWith(".json") ? extendsName : $"{extendsName}.json";
        return Path.Combine(this._directoryPath, baseName);
    }

    private void WriteSchema() {
        var directory = Path.GetDirectoryName(this.FilePath);
        if (directory == null) return;

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(this.FilePath);
        var schemaPath = Path.Combine(directory, $"{fileNameWithoutExtension}.schema.json");
        var schemaJson = this._schema.ToJson();
        File.WriteAllText(schemaPath, schemaJson);
    }
}
