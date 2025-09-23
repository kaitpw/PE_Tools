namespace AddinFamilyFoundrySuite.Core.MapValue;

/// <summary>
///     Storage type coercion strategy - handles cases where storage types differ but data types are compatible.
///     Implements comprehensive storage type conversions based on Revit's parameter system.
/// </summary>
public class StorageTypeCoercionStrategy : MappingStrategyBase {
    public StorageTypeCoercionStrategy(Document famDoc, FamilyParameter sourceParam, FamilyParameter targetParam) :
        base(famDoc, sourceParam, targetParam) {
    }

    public StorageTypeCoercionStrategy(Document famDoc, object sourceValue, FamilyParameter targetParam) :
        base(famDoc, sourceValue, targetParam) {
    }


    public override bool CanMap() => this.SourceStorageType == this.TargetStorageType
                                     || (this.SourceStorageType, this.TargetStorageType) switch {
                                         (StorageType.Integer, StorageType.String) => true,
                                         (StorageType.Integer, StorageType.Double) => true,
                                         (StorageType.Double, StorageType.String) => true,
                                         (StorageType.Double, StorageType.Integer) => true,
                                         (StorageType.String, StorageType.Integer) => Regexes.CanExtractInteger(
                                             this.SourceValue.ToString()),
                                         (StorageType.String, StorageType.Double) => Regexes.CanExtractDouble(
                                             this.SourceValue.ToString()),
                                         _ => false
                                     };

    public override Result<FamilyParameter> Map() {
        var convertedValue = (this.SourceStorageType, this.TargetStorageType) switch {
            // Same type - no conversion needed
            _ when this.SourceStorageType == this.TargetStorageType => this.SourceValue,

            // There is only one relevant SpecTypeId that stores as an integer: SpecTypeId.Int.Integer. 
            // Int.NumberOfPoles & Boolean.YesNo do too, but we can assume 
            // 1) that the user will not attempt this conversion and 2) that these are already "properly" set.
            (StorageType.Integer, StorageType.Double) => UnitUtils.ConvertToInternalUnits(
                this.SourceValue as int? ?? 0, this.TargetUnitType),

            // Safe to simply .ToString() on the integerParam's value
            (StorageType.Integer, StorageType.String) => this.SourceValue.ToString(),

            // Try to use the SourceValueString if it is available, otherwise fall back to ToString()
            (StorageType.Double, StorageType.String) => this.SourceValueString ?? this.SourceValue.ToString(),

            // Set to integer by extracting integer from the doubleParam's "value string"
            (StorageType.Double, StorageType.Integer) => Regexes.ExtractInteger(this.SourceValueString),

            // Set to integer by extracting integer from the stringParam's value
            (StorageType.String, StorageType.Integer) => Regexes.ExtractInteger(this.SourceValue.ToString()),

            // Set to double by extracting double from the stringParam's value
            (StorageType.String, StorageType.Double) => Regexes.ExtractDouble(this.SourceValue.ToString()),

            _ => throw new ArgumentException(
                $"Unsupported storage type conversion from {this.SourceStorageType} to {this.TargetStorageType}")
        };

        return this.FamilyManager.SetValueStrict(this.TargetParam, convertedValue);
    }
}