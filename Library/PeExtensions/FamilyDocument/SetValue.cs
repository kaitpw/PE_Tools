#nullable enable
using PeExtensions.FamDocument.SetValue;
using System.Globalization;

namespace PeExtensions.FamDocument;

public static class FamilyDocumentSetValue {
    /// <summary>
    ///     Sets a value on a parameter for ALL family types at once.
    ///     Supports strings (with optional units like "10'", "120V"), numbers, and already-parsed values.
    ///     Uses a formula workaround to avoid looping through each type.
    /// </summary>
    /// <param name="famDoc">The family document</param>
    /// <param name="param">The target parameter</param>
    /// <param name="value">Value to set - can be string (parsed based on parameter type), number, or typed value</param>
    /// <returns>True if the value was set successfully</returns>
    /// <exception cref="InvalidOperationException">Thrown if the StorageType is not supported or formula setting fails</exception>
    public static bool SetGlobalValue(this FamilyDocument famDoc, FamilyParameter param, object value) {
        // Parse string inputs into appropriate types
        if (value is string stringValue) {
            value = ParseStringValue(famDoc, param, stringValue);
        }

        var formula = ValueToFormulaString(famDoc, param, value);
        var success = famDoc.TrySetFormulaFast(param, formula, out var errorMessage);
        if (!success) throw new InvalidOperationException(errorMessage);
        return famDoc.UnsetFormula(param);
    }

    /// <summary>
    ///     Parses a string into the appropriate type based on parameter's StorageType.
    ///     For measurable specs, tries unit-formatted strings first (e.g., "10'", "120V"), then plain numbers.
    /// </summary>
    private static object ParseStringValue(FamilyDocument famDoc, FamilyParameter param, string input) {
        var dataType = param.Definition.GetDataType();

        return param.StorageType switch {
            StorageType.String => input,
            StorageType.Integer => int.Parse(input, CultureInfo.InvariantCulture),
            StorageType.Double when UnitUtils.IsMeasurableSpec(dataType) =>
                UnitFormatUtils.TryParse(famDoc.GetUnits(), dataType, input, out double parsed)
                    ? parsed
                    : double.Parse(input, CultureInfo.InvariantCulture),
            StorageType.Double => double.Parse(input, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(
                $"ParseStringValue not supported for parameter '{param.Definition.Name}' with StorageType.{param.StorageType}")
        };
    }

    /// <summary>
    ///     Converts a value to a formula string appropriate for the parameter's StorageType and DataType.
    /// </summary>
    private static string ValueToFormulaString(FamilyDocument famDoc, FamilyParameter param, object value) {
        var dataType = param.Definition.GetDataType();

        return param.StorageType switch {
            StorageType.String => $"\"{value}\"",
            StorageType.Integer => Convert.ToInt32(value).ToString(),
            StorageType.Double when UnitUtils.IsMeasurableSpec(dataType) =>
                UnitFormatUtils.Format(famDoc.GetUnits(), dataType, Convert.ToDouble(value), true),
            StorageType.Double => Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(
                $"ValueToFormulaString not supported for parameter '{param.Definition.Name}' with StorageType.{param.StorageType}")
        };
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

        var strategyInstance = ParamCoercionStrategyRegistry.Get(strategy.ToString());

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

        var strategyInstance = ValueCoercionStrategyRegistry.Get(strategy.ToString());

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