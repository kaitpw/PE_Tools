using PeExtensions.FamDocument.SetValue.CoercionStrategies;

namespace PeExtensions.FamDocument.SetValue;

public static class CoercionStrategyRegistry {
    private static readonly Dictionary<string, Func<Document, object, FamilyParameter, ICoercionStrategy>>
        _factories = new(StringComparer.OrdinalIgnoreCase) {
            ["AllowStorageTypeCoercion"] = (doc, value, target) => new CoerceByStorageType(doc, value, target),
            ["PeElectrical"] = (doc, value, target) => new CoerceElectrical(doc, value, target)
        };

    /// <summary>
    ///     Gets the strategy for a given policy name when coercing from source value to target parameter.
    /// </summary>
    public static ICoercionStrategy GetStrategy(
        string strategyName,
        Document document,
        object sourceValue,
        FamilyParameter targetParam
    ) => _factories.TryGetValue(strategyName, out var factory)
        ? factory(document, sourceValue, targetParam)
        : throw new ArgumentException(
            $"Unknown coercion strategy: {strategyName}. Available strategies: {string.Join(", ", _factories.Keys)}");

    public static List<string> GetAllStrategies() => _factories.Keys.ToList();
}

/// <summary>
///     Strategy that chains multiple strategies together. Tries each in order until one can handle the coercion.
/// </summary>
public class ChainedMappingStrategy : ICoercionStrategy {
    private readonly ICoercionStrategy[] _strategies;

    public ChainedMappingStrategy(params ICoercionStrategy[] strategies) {
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