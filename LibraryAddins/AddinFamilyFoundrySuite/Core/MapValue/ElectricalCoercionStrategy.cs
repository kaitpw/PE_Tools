namespace AddinFamilyFoundrySuite.Core.MapValue;

/// <summary>
///     Electrical coercion strategy - converts numeric/string values to electrical parameters with unit conversion.
/// </summary>
public class ElectricalCoercionStrategy : MappingStrategyBase {
    public ElectricalCoercionStrategy(Document famDoc, FamilyParameter sourceParam, FamilyParameter targetParam) :
        base(famDoc, sourceParam, targetParam) {
    }

    public ElectricalCoercionStrategy(Document famDoc, object sourceValue, FamilyParameter targetParam) :
        base(famDoc, sourceValue, targetParam) {
    }

    public override bool CanMap() {
        var isTargetElectrical = this.TargetDataType?.TypeId.Contains(".electrical:") == true;

        var isSourceSpecTypeValid = new[] { SpecTypeId.String.Text, SpecTypeId.Number, SpecTypeId.Int.Integer }
            .Contains(this.SourceDataType);

        return isTargetElectrical && isSourceSpecTypeValid;
    }

    public override Result<FamilyParameter> Map() {
        var currVal = this.SourceDataType switch {
            var t when t == SpecTypeId.String.Text => this.ExtractDouble(this.SourceValue.ToString()),
            var t when t == SpecTypeId.Number => this.SourceValue as double? ?? 0,
            var t when t == SpecTypeId.Int.Integer => this.SourceValue as int? ?? 0,
            _ => throw new ArgumentException($"Unsupported source type {this.SourceDataType} for electrical coercion")
        };

        var convertedVal = UnitUtils.ConvertToInternalUnits(currVal, this.TargetUnitType);

        return this.FamilyManager.SetValueStrict(this.TargetParam, convertedVal);
    }

    public double ExtractDouble(string sourceValue) {
        if (TargetParam.Definition.Name.Contains("Voltage", StringComparison.OrdinalIgnoreCase)) {
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