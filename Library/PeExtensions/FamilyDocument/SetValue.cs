using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamDocument.SetValue.CoercionStrategies;

namespace PeExtensions.FamDocument;

public static class FamilyDocumentSetValue {
    /// <summary>
    ///     Set a family's parameter value on the <c>FamilyManager.CurrentType</c> using the specified strategy.
    ///     If no strategy is specified, uses the <c>Strict</c> strategy.
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    public static FamilyParameter SetValue(this Document famDoc,
        FamilyParameter targetParam,
        FamilyParameter sourceParam,
        ParamCoercionStrategy strategy = ParamCoercionStrategy.Strict
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");

        var context = CoercionContext.FromParam(famDoc, sourceParam, targetParam);
        ICoercionStrategy strategyInstance = strategy switch {
            ParamCoercionStrategy.Strict => new Strict(),
            ParamCoercionStrategy.CoerceByStorageType => new CoerceByStorageType(),
            ParamCoercionStrategy.CoerceElectrical => new CoerceElectrical(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy,
                $"Unknown strategy. Options are: {string.Join(", ", Enum.GetNames(typeof(ParamCoercionStrategy)))}")
        };

        if (!strategyInstance.CanMap(context)) {
            var targetDataType = targetParam?.Definition.GetDataType();
            throw new Exception(
                $"Cannot map '{sourceParam.Definition.Name}' to '{targetParam.Definition.Name}' ({targetDataType}) using strategy '{strategy}'");
        }

        var (param, err) = strategyInstance.Map(context);
        if (err is not null) throw err;
        return param;
    }

    /// <summary>
    ///     Set a family's parameter value on the <c>FamilyManager.CurrentType</c> using the specified strategy.
    ///     If no strategy is specified, uses the <c>Strict</c> strategy.
    /// </summary>
    /// <remarks>
    ///     YOU MUST set FamilyManager.CurrentType BEFORE using this method. Both getting and setting CurrentType
    ///     are VERY expensive operations, thus it is not done inside this method. Do it at the highest-level possible
    ///     in your loop/s.
    /// </remarks>
    public static FamilyParameter SetValue(this Document famDoc,
        FamilyParameter targetParam,
        object sourceValue,
        ValueCoercionStrategy strategy = ValueCoercionStrategy.Strict
    ) {
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document");

        var context = CoercionContext.FromValue(famDoc, sourceValue, targetParam);
        ICoercionStrategy strategyInstance = strategy switch {
            ValueCoercionStrategy.Strict => new Strict(),
            ValueCoercionStrategy.CoerceSimple => new CoerceSimple(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy,
                $"Unknown strategy. Options are: {string.Join(", ", Enum.GetNames(typeof(ValueCoercionStrategy)))}"
            )
        };

        if (!strategyInstance.CanMap(context)) {
            var targetDataType = targetParam?.Definition.GetDataType();
            throw new Exception(
                $"Cannot map value '{sourceValue}' to '{targetParam.Definition.Name}' ({targetDataType}) using strategy '{strategy}'");
        }

        var (param, err) = strategyInstance.Map(context);
        if (err is not null) throw err;
        return param;
    }
}