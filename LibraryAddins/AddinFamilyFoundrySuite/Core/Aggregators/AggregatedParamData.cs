using Nice3point.Revit.Extensions;

namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Aggregated parameter data across multiple families for CSV output.
/// </summary>
public class AggregatedParamData {
    public AggregatedParamData(ParamCollectionResult param) {
        this.ParamName = param.ParamName;
        this.DataTypeLabel = param.DataType.ToLabel();
        this.ForgeTypeId = param.DataType?.TypeId ?? string.Empty;
        this.StorageType = param.StorageType.ToString();
        this.IsInstance = param.IsInstance;
        this.IsBuiltIn = param.IsBuiltIn;
        this.SharedGuid = param.SharedGuid?.ToString() ?? string.Empty;
        this.FamilyNames = [];
    }

    /// <summary>
    ///     The parameter's display name
    /// </summary>
    public string ParamName { get; init; }

    /// <summary>
    ///     Number of families that have this parameter
    /// </summary>
    public int FamilyCount { get; set; }

    /// <summary>
    ///     List of family names that have this parameter
    /// </summary>
    public List<string> FamilyNames { get; set; } = [];

    /// <summary>
    ///     Human-readable label for the data type (via LabelUtils.GetLabelForSpec)
    /// </summary>
    public string DataTypeLabel { get; init; }

    /// <summary>
    ///     The ForgeTypeId string for the data type
    /// </summary>
    public string ForgeTypeId { get; set; } = string.Empty;

    /// <summary>
    ///     True if instance parameter, false if type parameter
    /// </summary>
    public bool IsInstance { get; init; }

    /// <summary>
    ///     The internal storage type (Double, Integer, String, ElementId)
    /// </summary>
    public string StorageType { get; init; }

    /// <summary>
    ///     True if this is a built-in Revit parameter
    /// </summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    ///     The shared parameter GUID if this is a shared parameter, empty string otherwise
    /// </summary>
    public string SharedGuid { get; init; }
}
