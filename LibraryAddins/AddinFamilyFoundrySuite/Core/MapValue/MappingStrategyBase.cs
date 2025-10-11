#nullable enable
namespace AddinFamilyFoundrySuite.Core.MapValue;

/// <summary>
///     Interface for parameter mapping strategies.
///     Each strategy decides if it can handle a mapping and executes the mapping.
/// </summary>
public interface IMappingStrategy {
    bool CanMap();
    Result<FamilyParameter> Map();
}

/// <summary>
///     Base class for complex parameter mapping strategies to reduce code duplication/fragility.
///     Implements the IMappingStrategy interface and predefines useful properties.
///     If the strategy is simple, implement the IMappingStrategy interface directly to reduce per overhead.
/// </summary>
public abstract class MappingStrategyBase : IMappingStrategy {
    /// <summary>
    ///     Constructor for mapping from source parameter to target parameter
    /// </summary>
    protected MappingStrategyBase(Document famDoc, FamilyParameter sourceParam, FamilyParameter targetParam) {
        this.FamilyManager = famDoc.FamilyManager;

        this.SourceValue = this.FamilyManager.GetValue(sourceParam);
        this.SourceValueString = this.FamilyManager.CurrentType.AsValueString(sourceParam);
        this.SourceDataType = sourceParam.Definition.GetDataType();

        this.TargetParam = targetParam;
        this.TargetDataType = targetParam.Definition.GetDataType();
        this.TargetStorageType = targetParam.StorageType;
        try {
            this.TargetUnitType = famDoc.GetUnits().GetFormatOptions(this.TargetDataType).GetUnitTypeId();
        } catch {
        } // ignore "specTypeId is not a measurable spec identifier. See UnitUtils.IsMeasurableSpec(ForgeTypeId)"
    }

    /// <summary>
    ///     Constructor for mapping from direct value to target parameter
    /// </summary>
    protected MappingStrategyBase(Document famDoc, object sourceValue, FamilyParameter targetParam) {
        this.FamilyManager = famDoc.FamilyManager;

        this.SourceValue = sourceValue;
        this.SourceValueString = null;
        this.SourceDataType = null;

        this.TargetParam = targetParam;
        this.TargetDataType = targetParam.Definition.GetDataType();
        this.TargetStorageType = targetParam.StorageType;
        try {
            this.TargetUnitType = famDoc.GetUnits().GetFormatOptions(this.TargetDataType).GetUnitTypeId();
        } catch {
        } // ignore "specTypeId is not a measurable spec identifier. See UnitUtils.IsMeasurableSpec(ForgeTypeId)"
    }

    public FamilyManager FamilyManager { get; protected init; }

    /// <summary>
    ///     The SourceValue to map into the TargetParam. The SourceParam itself should not be stored because
    ///     all mapping strategies should also be able to accept a source value directly.
    /// </summary>
    public object SourceValue { get; protected init; }


    /// <summary>
    ///     The string representation of the family parameter's internally stored value.
    ///     Obtained with FamilyManager.CurrentType.AsValueString(sourceParam) which returns a unit'ed string (e.g "10.000 m")
    ///     Will be <see langword="null" /> if the strategy is initialized with a direct value instead of a source parameter.
    /// </summary>
    public string? SourceValueString { get; private set; }


    /// <summary>
    ///     The data type of the source parameter from FamilyParameter.Definition.GetDataType().
    ///     Will be <see langword="null" /> (default) if the strategy is initialized with a direct value instead of a source
    ///     parameter.
    /// </summary>
    public ForgeTypeId? SourceDataType { get; private set; }


    /// <summary>
    ///     The storage type of the source parameter.Normally obtained from FamilyParameter.StorageType
    ///     however here it is derived from the source value in order to support initialization with direct values.
    /// </summary>
    public StorageType SourceStorageType =>
        this.SourceValue switch {
            double => StorageType.Double,
            int => StorageType.Integer,
            string => StorageType.String,
            ElementId => StorageType.ElementId,
            _ => throw new ArgumentException($"Invalid type of value to set ({this.SourceValue.GetType().Name})")
        };

    /// <summary>
    ///     The TargetParam to map the SourceValue into. This must be stored so that it can be set
    /// </summary>
    public FamilyParameter TargetParam { get; protected init; }

    /// <summary>
    ///     The data type of the target parameter from FamilyParameter.Definition.GetDataType()
    /// </summary>
    public ForgeTypeId TargetDataType { get; protected init; }

    /// <summary>
    ///     The storage type of the target parameter from FamilyParameter.StorageType
    /// </summary>
    public StorageType TargetStorageType { get; protected init; }

    /// <summary>
    ///     The unit type id of the target parameter from
    ///     FamilyParameter.Units.GetFormatOptions(TargetDataType).GetUnitTypeId().
    ///     Use this to convert the source value to the target parameter's internal storage type.
    ///     <code>
    /// var convertedVal = UnitUtils.ConvertToInternalUnits(sourceValue, this.TargetUnitType);
    /// this.FamilyManager.SetValueStrict(this.TargetParam, convertedVal);
    /// </code>
    /// </summary>
    public ForgeTypeId? TargetUnitType { get; protected init; }

    /// <summary>
    ///     Determines if this strategy can handle the given parameter mapping scenario.
    /// </summary>
    public abstract bool CanMap();

    /// <summary>
    ///     Executes the mapping. Should only be called if CanMap returns true.
    /// </summary>
    public abstract Result<FamilyParameter> Map();
}