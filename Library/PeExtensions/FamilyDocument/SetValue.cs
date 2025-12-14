#nullable enable
using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamDocument.SetValue.CoercionStrategies;
using System.Globalization;

namespace PeExtensions.FamDocument;

public static class FamilyDocumentSetValue {
    /// <summary>
    ///     Sets a value on a parameter for ALL family types at once.
    ///     Uses a formula workaround to avoid looping through each type.
    ///     Automatically converts the value to the appropriate formula format based on the parameter's StorageType.
    /// </summary>
    /// <param name="famDoc">The family document</param>
    /// <param name="param">The target parameter</param>
    /// <param name="value">The value to set (will be coerced based on parameter's StorageType)</param>
    /// <returns>The parameter if the value was set successfully, otherwise null</returns>
    /// <exception cref="System.InvalidOperationException">Thrown if the StorageType is not supported</exception>
    /// <summary>
    ///     Converts a value to a formula string appropriate for the parameter's StorageType and DataType.
    ///     Handles unit formatting for measurable specs (Length, Voltage, etc.).
    /// </summary>
    /// <remarks>
    ///     The value should be in Revit's internal units (feet, radians, etc.) for Double parameters.
    ///     This method will format it with the document's display units for Revit to parse.
    /// </remarks>
    private static string ValueToFormulaString(FamilyDocument famDoc, FamilyParameter param, object value) {
        var dataType = param.Definition.GetDataType();

        return param.StorageType switch {
            StorageType.String => $"\"{value}\"",
            StorageType.Integer => Convert.ToInt32(value).ToString(),
            StorageType.Double when UnitUtils.IsMeasurableSpec(dataType) =>
                UnitFormatUtils.Format(famDoc.GetUnits(), dataType, Convert.ToDouble(value), true),
            StorageType.Double =>
                Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(
                $"SetGlobalValue not supported for parameter '{param.Definition.Name}' with StorageType.{param.StorageType}")
        };
    }

    public static bool SetGlobalValue(this FamilyDocument famDoc, FamilyParameter param, object value) {
        var formula = ValueToFormulaString(famDoc, param, value);

        var success = famDoc.TrySetFormulaFast(param, formula, out var errorMessage);
        if (!success) throw new InvalidOperationException(errorMessage);
        return famDoc.UnsetFormula(param);
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
    /// <returns>
    ///     The mapped (target) parameter, or null if the source value is null.
    /// </returns>
    public static FamilyParameter? SetValue(
        this FamilyDocument famDoc,
        FamilyParameter targetParam,
        FamilyParameter sourceParam,
        ParamCoercionStrategy strategy = ParamCoercionStrategy.Strict
    ) {
        var context = CoercionContext.FromParam(famDoc, sourceParam, targetParam);
        if (context.SourceValue == null) return null;

        ICoercionStrategy strategyInstance = strategy switch {
            ParamCoercionStrategy.Strict => new Strict(),
            ParamCoercionStrategy.CoerceByStorageType => new CoerceByStorageType(),
            ParamCoercionStrategy.CoerceElectrical => new CoerceElectrical(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy,
                $"Unknown strategy. Options are: {string.Join(", ", Enum.GetNames(typeof(ParamCoercionStrategy)))}")
        };

        if (!strategyInstance.CanMap(context)) {
            var targetDataType = targetParam?.Definition.GetDataType();
            var dataTypeDisplay = targetDataType?.TypeId ?? "Unknown";
            throw new Exception(
                $"Cannot map '{sourceParam.Definition.Name}' to '{targetParam.Definition.Name}' ({dataTypeDisplay}) using strategy '{strategy}'");
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
    /// <returns>
    ///     The mapped (target) parameter, or null if the source value is null.
    /// </returns>
    public static FamilyParameter? SetValue(
        this FamilyDocument famDoc,
        FamilyParameter targetParam,
        object sourceValue,
        ValueCoercionStrategy strategy = ValueCoercionStrategy.Strict
    ) {
        var context = CoercionContext.FromValue(famDoc, sourceValue, targetParam);
        if (context.SourceValue == null) return null;

        ICoercionStrategy strategyInstance = strategy switch {
            ValueCoercionStrategy.Strict => new Strict(),
            ValueCoercionStrategy.CoerceSimple => new CoerceSimple(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy,
                $"Unknown strategy. Options are: {string.Join(", ", Enum.GetNames(typeof(ValueCoercionStrategy)))}"
            )
        };

        if (!strategyInstance.CanMap(context)) {
            var targetDataType = targetParam?.Definition.GetDataType();
            var dataTypeDisplay = targetDataType?.TypeId ?? "Unknown";
            throw new Exception(
                $"Cannot map value '{sourceValue}' to '{targetParam.Definition.Name}' ({dataTypeDisplay}) using strategy '{strategy}'");
        }

        var (param, err) = strategyInstance.Map(context);
        if (err is not null) throw err;
        return param;
    }
}