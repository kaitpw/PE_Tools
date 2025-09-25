namespace AddinFamilyFoundrySuite.Core.MapValue;

/// <summary>
///     Registry for mapping policies. Maps policy names to strategy factory functions.
///     Keeps things simple without heavy DI frameworks.
/// </summary>
public static class MappingPolicyRegistry {
    private static readonly Dictionary<string, Func<Document, FamilyParameter, FamilyParameter, IMappingStrategy>>
        _strategyFactories = new(StringComparer.OrdinalIgnoreCase) {
            ["Strict"] = (doc, source, target) => new StrictMappingStrategy(doc, source, target),
            ["AllowStorageTypeCoercion"] =
                (doc, source, target) => new StorageTypeCoercionStrategy(doc, source, target),
            ["AllowElectricalCoercion"] = (doc, source, target) => new ChainedMappingStrategy(
                new StrictMappingStrategy(doc, source, target),
                new ElectricalCoercionStrategy(doc, source, target)
            ),
            ["AllowAllCoercion"] = (doc, source, target) => new ChainedMappingStrategy(
                new ElectricalCoercionStrategy(doc, source, target),
                new StorageTypeCoercionStrategy(doc, source, target)
            )
        };

    private static readonly Dictionary<string, Func<Document, object, FamilyParameter, IMappingStrategy>>
        _valueStrategyFactories = new(StringComparer.OrdinalIgnoreCase) {
            ["Strict"] = (doc, value, target) => new StrictMappingStrategy(doc, value, target),
            ["AllowStorageTypeCoercion"] = (doc, value, target) => new StorageTypeCoercionStrategy(doc, value, target),
            ["AllowElectricalCoercion"] = (doc, value, target) => new ChainedMappingStrategy(
                new StrictMappingStrategy(doc, value, target),
                new ElectricalCoercionStrategy(doc, value, target)
            ),
            ["AllowAllCoercion"] = (doc, value, target) => new ChainedMappingStrategy(
                new ElectricalCoercionStrategy(doc, value, target),
                new StorageTypeCoercionStrategy(doc, value, target)
            )
        };

    /// <summary>
    ///     Gets the strategy for a given policy name when mapping from source parameter to target parameter.
    /// </summary>
    public static IMappingStrategy GetStrategy(string policyName,
        Document document,
        FamilyParameter sourceParam,
        FamilyParameter targetParam) {
        if (string.IsNullOrWhiteSpace(policyName)) policyName = "Strict"; // Default fallback

        return _strategyFactories.TryGetValue(policyName, out var factory)
            ? factory(document, sourceParam, targetParam)
            : throw new ArgumentException(
                $"Unknown mapping policy: {policyName}. Available policies: {string.Join(", ", _strategyFactories.Keys)}");
    }

    /// <summary>
    ///     Gets the strategy for a given policy name when mapping from source value to target parameter.
    /// </summary>
    public static IMappingStrategy GetStrategy(string policyName,
        Document document,
        object sourceValue,
        FamilyParameter targetParam) {
        if (string.IsNullOrWhiteSpace(policyName)) policyName = "Strict"; // Default fallback

        return _valueStrategyFactories.TryGetValue(policyName, out var factory)
            ? factory(document, sourceValue, targetParam)
            : throw new ArgumentException(
                $"Unknown mapping policy: {policyName}. Available policies: {string.Join(", ", _valueStrategyFactories.Keys)}");
    }

    public static List<string> GetAllPolicies() => _strategyFactories.Keys.ToList();
}

/// <summary>
///     Strategy that chains multiple strategies together. Tries each in order until one can handle the mapping.
/// </summary>
public class ChainedMappingStrategy : IMappingStrategy {
    private readonly IMappingStrategy[] _strategies;

    public ChainedMappingStrategy(params IMappingStrategy[] strategies) {
        this._strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
        if (this._strategies.Length == 0)
            throw new ArgumentException("At least one strategy must be provided", nameof(strategies));
    }

    public bool CanMap() => this._strategies.Any(s => s.CanMap());

    public Result<FamilyParameter> Map() {
        foreach (var strategy in this._strategies) {
            if (strategy.CanMap())
                return strategy.Map();
        }

        return new Exception("Cannot map value - no suitable strategy found in chain");
    }
}