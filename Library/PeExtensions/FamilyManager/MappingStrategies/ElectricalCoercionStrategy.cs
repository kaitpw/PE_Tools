using PeUtils;

namespace PeExtensions;

/// <summary>
///     Electrical coercion strategy - converts numeric/string values to electrical parameters with unit conversion.
/// </summary>
public class ElectricalCoercionStrategy : MappingStrategyBase {
    public ElectricalCoercionStrategy(Document famDoc, FamilyParameter sourceParam, FamilyParameter targetParam) :
        base(famDoc, sourceParam, targetParam) { }

    public ElectricalCoercionStrategy(Document famDoc, object sourceValue, FamilyParameter targetParam) :
        base(famDoc, sourceValue, targetParam) { }

    public override bool CanMap() {
        var isTargetElectrical = this.TargetDataType?.TypeId.Contains(".electrical:") == true;

        var isSourceSpecTypeValid = new[] { SpecTypeId.String.Text, SpecTypeId.Number, SpecTypeId.Int.Integer }
            .Contains(this.SourceDataType);

        return isTargetElectrical && isSourceSpecTypeValid;
    }

    public override Result<FamilyParameter> Map() {
        var currVal = this.SourceDataType switch {
            var t when t == SpecTypeId.String.Text => Regexes.ExtractDouble(this.SourceValue.ToString()),
            var t when t == SpecTypeId.Number => this.SourceValue as double? ?? 0,
            var t when t == SpecTypeId.Int.Integer => this.SourceValue as int? ?? 0,
            _ => throw new ArgumentException($"Unsupported source type {this.SourceDataType} for electrical coercion")
        };

        var convertedVal = UnitUtils.ConvertToInternalUnits(currVal, this.TargetUnitType);

        return this.FamilyManager.SetValueStrict(this.TargetParam, convertedVal);
    }
}
