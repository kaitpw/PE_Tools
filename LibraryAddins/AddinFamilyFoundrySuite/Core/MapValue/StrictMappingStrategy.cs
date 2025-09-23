namespace AddinFamilyFoundrySuite.Core.MapValue;

/// <summary>
///     Strict mapping strategy - only maps when data types are identical.
///     Implemented as an IMappingStrategy interface for perf reasons.
/// </summary>
public class StrictMappingStrategy : IMappingStrategy {
    public StrictMappingStrategy(Document famDoc, FamilyParameter sourceParam, FamilyParameter targetParam) {
        this.FamilyManager = famDoc.FamilyManager;
        this.SourceValue = this.FamilyManager.GetValue(sourceParam);
        this.SourceDataType = sourceParam.Definition.GetDataType();
        this.TargetDataType = targetParam.Definition.GetDataType();
        this.TargetParam = targetParam;
    }

    public StrictMappingStrategy(Document famDoc, object sourceValue, FamilyParameter targetParam) {
        this.FamilyManager = famDoc.FamilyManager;
        this.SourceValue = sourceValue;
        this.SourceDataType = null; // No source parameter, so no data type available
        this.TargetDataType = targetParam.Definition.GetDataType();
        this.TargetParam = targetParam;
    } // intentionally implements IMappingStrategy interface for perf reasons.

    private FamilyManager FamilyManager { get; }
    public object SourceValue { get; init; }
    public ForgeTypeId? SourceDataType { get; init; }
    public ForgeTypeId TargetDataType { get; init; }
    public FamilyParameter TargetParam { get; init; }

    public bool CanMap() {
        // When mapping from a direct value (no source parameter), we can't do strict type checking
        // since we don't have the source data type. Let the strategy attempt the mapping.
        if (this.SourceDataType == null) return true;

        return this.SourceDataType == this.TargetDataType;
    }

    public Result<FamilyParameter> Map() => this.FamilyManager.SetValueStrict(this.TargetParam, this.SourceValue);
}