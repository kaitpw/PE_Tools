namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Aggregated parameter data across multiple families for CSV output.
/// </summary>
public class AggregatedParamData
{
    /// <summary>
    ///     The parameter's display name
    /// </summary>
    public string ParamName { get; set; } = string.Empty;

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
    public string DataTypeLabel { get; set; } = string.Empty;

    /// <summary>
    ///     The ForgeTypeId string for the data type
    /// </summary>
    public string ForgeTypeId { get; set; } = string.Empty;

    /// <summary>
    ///     True if instance parameter, false if type parameter
    /// </summary>
    public bool IsInstance { get; set; }

    /// <summary>
    ///     The internal storage type (Double, Integer, String, ElementId)
    /// </summary>
    public string StorageType { get; set; } = string.Empty;

    /// <summary>
    ///     True if this is a built-in Revit parameter
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    ///     The shared parameter GUID if this is a shared parameter, empty string otherwise
    /// </summary>
    public string SharedGuid { get; set; } = string.Empty;
}
