using PeExtensions.FamDocument.SetValue.CoercionStrategies;

namespace PeExtensions.FamDocument.SetValue;

public static class ParamCoercionStrategyRegistry {
    private static readonly Dictionary<string, Func<Document, FamilyParameter, FamilyParameter, IBaseCoercionStrategy>>
        _factories = new(StringComparer.OrdinalIgnoreCase) {
            [nameof(CoerceByStorageType)] = (doc, source, target) => new CoerceByStorageType(doc, source, target),
            [nameof(CoerceElectrical)] = (doc, source, target) => new CoerceElectrical(doc, source, target)
        };

    /// <summary>
    ///     Gets the strategy for a given policy name when coercing from source value to target parameter.
    /// </summary>
    public static IBaseCoercionStrategy GetStrategy(
        string strategyName,
        Document document,
        FamilyParameter sourceParam,
        FamilyParameter targetParam
    ) => _factories.TryGetValue(strategyName, out var factory)
        ? factory(document, sourceParam, targetParam)
        : throw new ArgumentException(
            $"Unknown coercion strategy: {strategyName}. Available strategies: {string.Join(", ", _factories.Keys)}");

    public static List<string> GetAllStrategies() => _factories.Keys.ToList();
}

/// <summary>
///     Strategy that chains multiple strategies together. Tries each in order until one can handle the coercion.
/// </summary>
public class ChainedMappingStrategy : IBaseCoercionStrategy {
    private readonly IBaseCoercionStrategy[] _strategies;

    public ChainedMappingStrategy(params IBaseCoercionStrategy[] strategies) {
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