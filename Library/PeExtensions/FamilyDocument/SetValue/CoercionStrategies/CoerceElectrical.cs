#nullable enable
using PeExtensions.FamDocument.SetValue.Utils;

namespace PeExtensions.FamDocument.SetValue.CoercionStrategies;

/// <summary>
///     Electrical coercion strategy - converts numeric/string values to electrical parameters with unit conversion.
/// </summary>
public class CoerceElectrical : ICoercionStrategy {
    public bool CanMap(CoercionContext context) {
        var isTargetElectrical = context.TargetDataType?.TypeId.Contains(".electrical:") == true;
        var canExtractDouble = Regexes.TryExtractDouble(context.SourceValue.ToString(), out _);
        return isTargetElectrical && canExtractDouble;
    }

    public Result<FamilyParameter> Map(CoercionContext context) {
        var currVal = context.SourceDataType switch {
            var t when t == SpecTypeId.String.Text => this.ExtractDouble(context.SourceValue.ToString() ?? string.Empty,
                context.TargetParam),
            var t when t == SpecTypeId.Number => context.SourceValue as double? ?? 0,
            var t when t == SpecTypeId.Int.Integer => context.SourceValue as int? ?? 0,
            _ => throw new ArgumentException(
                $"Unsupported source type {context.SourceDataType} for electrical coercion")
        };

        var convertedVal = UnitUtils.ConvertToInternalUnits(currVal, context.TargetUnitType);

        return context.FamilyDocument.SetValue(context.TargetParam, convertedVal);
    }

    private double ExtractDouble(string sourceValue, FamilyParameter targetParam) {
        if (targetParam.Definition.Name.Contains("Voltage", StringComparison.OrdinalIgnoreCase)) {
            // somewhat arbitrary ranges. 240 must account for 230. 120 must account for 110 or 115.
            var voltRange240 = Enumerable.Range(225, 21).Select(x => (double)x).ToList();
            voltRange240.Add(208);
            var voltRange120 = Enumerable.Range(107, 15).Select(x => (double)x).ToList();
            if (voltRange240.Any(x => sourceValue.Contains(x.ToString()))) return 240;
            if (voltRange120.Any(x => sourceValue.Contains(x.ToString()))) return 120;
        }

        return Regexes.ExtractDouble(sourceValue);
    }
}