using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using PeServices.Storage.Core.Json.SchemaProviders;

namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Aggregated parameter data across multiple families for CSV output.
/// </summary>
public class AggregatedParamData(ParamSnapshot param) {
    /// <summary> Human-readable label for the data type (via LabelUtils.GetLabelForSpec) </summary>
    public string DataType = SpecNamesProvider.GetLabelMap()
        .ToDictionary(kvp => kvp.Value, kvp => kvp.Key)
        .TryGetValue(param.DataType, out var label)
        ? label
        : string.Empty;


    /// <summary> The ForgeTypeId string for the data type </summary>
    public string DataTypeId = param.DataType.TypeId;

    /// <summary>True if this is a built-in Revit parameter </summary>
    public bool IsBuiltIn = param.IsBuiltIn;

    /// <summary> True if instance parameter, false if type parameter </summary>
    public bool IsInstance = param.IsInstance;

    /// <summary> True if this is a project parameter (which only exists in the project document). False otherwise </summary>
    public bool IsProjectParameter = param.IsProjectParameter;


    /// <summary> The parameter's display name </summary>
    public string ParamName = param.Name;

    /// <summary> The shared parameter GUID if this is a shared parameter, empty string otherwise </summary>
    public string SharedGuid = param.SharedGuid.ToString();

    /// <summary> The internal storage type (Double, Integer, String, ElementId) </summary>
    public string StorageType = param.StorageType.ToString();

    /// <summary> True if this is a family parameter, false if a project parameter </summary>
    public bool IsFamilyParameter => string.IsNullOrWhiteSpace(this.SharedGuid) && !this.IsBuiltIn;

    /// <summary> Number of families that have this parameter </summary>
    public int FamilyCount => this.FamilyNames.Count;

    /// <summary> List of family names that have this parameter </summary>
    public List<string> FamilyNames { get; set; } = [];

    /// <summary> Number of schedules that include this parameter </summary>
    public int ScheduleCount => this.ScheduleNames.Count;

    /// <summary> List of schedule names that include this parameter </summary>
    public List<string> ScheduleNames { get; set; } = [];

    /// <summary> Distinct family categories that contain this parameter </summary>
    public HashSet<string> FamilyCategories { get; set; } = [];

    /// <summary> Distinct schedule categories that contain this parameter </summary>
    public HashSet<string> ScheduleCategories { get; set; } = [];
}