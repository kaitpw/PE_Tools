using NJsonSchema;
using PeServices.Storage.Core.Json.Converters;
using PeServices.Storage.Core.Json.RevitTypes;
using PeServices.Storage.Core.Json.SchemaProcessors;
using PeServices.Storage.Core.Json.SchemaProviders;

namespace PeServices.Storage.Core.Json;

/// <summary>
///     Registration information for a Revit-native type that needs custom JSON schema generation.
/// </summary>
public class TypeRegistration {
    /// <summary> The JSON Schema type this C# type should be represented as </summary>
    public required JsonObjectType SchemaType { get; init; }

    /// <summary> Optional: Type of discriminator attribute (e.g., ForgeKindAttribute for ForgeTypeId) </summary>
    public Type DiscriminatorType { get; init; }

    /// <summary> Optional: Function to select provider based on discriminator attribute </summary>
    public Func<Attribute, Type> ProviderSelector { get; init; }

    /// <summary> Optional: Function to select converter type based on discriminator attribute </summary>
    public Func<Attribute, Type> ConverterSelector { get; init; }

    /// <summary> Optional: Default provider when no discriminator is present </summary>
    public Type DefaultProvider { get; init; }

    /// <summary> Optional: Default converter when no discriminator is present </summary>
    public Type DefaultConverter { get; init; }
}

/// <summary>
///     Central registry for all Revit-native types that need custom JSON schema generation.
///     This registry maps C# types to their JSON schema representation and associated providers.
/// </summary>
public static class RevitTypeRegistry {
    private static readonly Dictionary<Type, TypeRegistration> _registrations = new();
    private static bool _initialized;

    /// <summary>
    ///     Initialize the registry with all known Revit type mappings.
    ///     Safe to call multiple times (only runs once).
    /// </summary>
    public static void Initialize() {
        if (_initialized) return;

        // ForgeTypeId with discriminator - requires [ForgeKind] attribute
        Register<ForgeTypeId>(new TypeRegistration {
            SchemaType = JsonObjectType.String,
            DiscriminatorType = typeof(ForgeKindAttribute),
            ProviderSelector = attr => attr switch {
                ForgeKindAttribute { Kind: ForgeKind.Spec } => typeof(SpecNamesProvider),
                ForgeKindAttribute { Kind: ForgeKind.Group } => typeof(PropertyGroupNamesProvider),
                _ => null
            },
            ConverterSelector = attr => attr switch {
                ForgeKindAttribute { Kind: ForgeKind.Spec } => typeof(SpecTypeConverter),
                ForgeKindAttribute { Kind: ForgeKind.Group } => typeof(GroupTypeConverter),
                _ => null
            }
        });

        // Category without discriminator - always uses CategoryNamesProvider
        Register<Category>(new TypeRegistration {
            SchemaType = JsonObjectType.String,
            DefaultProvider = typeof(CategoryNamesProvider),
            DefaultConverter = typeof(CategoryConverter)
        });

        _initialized = true;
    }

    /// <summary>
    ///     Register a type with its schema configuration.
    /// </summary>
    private static void Register<T>(TypeRegistration registration) =>
        _registrations[typeof(T)] = registration;

    /// <summary>
    ///     Try to get registration for a type.
    /// </summary>
    public static bool TryGet(Type type, out TypeRegistration registration) =>
        // Direct match
        _registrations.TryGetValue(type, out registration) ||
        // Fallback: match by type name (for cross-assembly scenarios)
        (registration = _registrations.Values.FirstOrDefault(r =>
            _registrations.Keys.Any(k => k.Name == type.Name))) != null;

    /// <summary>
    ///     Clear all registrations (useful for testing).
    /// </summary>
    public static void Clear() {
        _registrations.Clear();
        _initialized = false;
    }
}
