#nullable enable
namespace PeExtensions.FamDocument.SetValue;

/// <summary>
///     Context object containing all data needed for parameter coercion strategies.
///     Supports both value-to-param and param-to-param mapping scenarios.
/// </summary>
public record CoercionContext {
    public required Document FamilyDocument { get; init; }
    public required FamilyManager FamilyManager { get; init; }
    public required FamilyParameter TargetParam { get; init; }
    public required object SourceValue { get; init; }

    /// <summary>
    ///     The string representation of the source parameter's internally stored value.
    ///     Only populated for param-to-param mapping. Will be null for value-to-param mapping.
    /// </summary>
    public string? SourceValueString { get; init; }

    /// <summary>
    ///     The data type of the source parameter from FamilyParameter.Definition.GetDataType().
    ///     Only populated for param-to-param mapping. Will be null for value-to-param mapping.
    /// </summary>
    public ForgeTypeId? SourceDataType { get; init; }

    /// <summary>
    ///     The storage type of the source value, derived from the value's type.
    /// </summary>
    public StorageType SourceStorageType =>
        this.SourceValue switch {
            double => StorageType.Double,
            int => StorageType.Integer,
            string => StorageType.String,
            ElementId => StorageType.ElementId,
            _ => throw new ArgumentException($"Invalid source value type ({this.SourceValue.GetType().Name})")
        };

    /// <summary>
    ///     The storage type of the target parameter from FamilyParameter.StorageType
    /// </summary>
    public StorageType TargetStorageType => this.TargetParam.StorageType;

    /// <summary>
    ///     The data type of the target parameter from FamilyParameter.Definition.GetDataType()
    /// </summary>
    public ForgeTypeId TargetDataType => this.TargetParam.Definition.GetDataType();

    /// <summary>
    ///     The unit type id of the target parameter.
    ///     Use this to convert the source value to the target parameter's internal storage type.
    ///     <code>
    /// var convertedVal = UnitUtils.ConvertToInternalUnits(sourceValue, context.TargetUnitType);
    /// context.FamilyDocument.SetValueStrict(context.TargetParam, convertedVal);
    /// </code>
    /// </summary>
    public ForgeTypeId? TargetUnitType {
        get {
            try {
                return this.FamilyDocument.GetUnits()
                    .GetFormatOptions(this.TargetDataType)
                    .GetUnitTypeId();
            } catch {
                return null; // not a measurable spec identifier
            }
        }
    }

    /// <summary>
    ///     Factory method for creating a context from a direct value to target parameter mapping.
    /// </summary>
    public static CoercionContext FromValue(Document doc, object sourceValue, FamilyParameter targetParam) =>
        new() {
            FamilyDocument = doc,
            FamilyManager = doc.FamilyManager,
            SourceValue = sourceValue,
            TargetParam = targetParam
        };

    /// <summary>
    ///     Factory method for creating a context from a source parameter to target parameter mapping.
    /// </summary>
    public static CoercionContext FromParam(Document doc, FamilyParameter sourceParam, FamilyParameter targetParam) =>
        new() {
            FamilyDocument = doc,
            FamilyManager = doc.FamilyManager,
            SourceValue = doc.GetValue(sourceParam),
            SourceValueString = doc.FamilyManager.CurrentType.AsValueString(sourceParam),
            SourceDataType = sourceParam.Definition.GetDataType(),
            TargetParam = targetParam
        };
}