using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using PeServices.Storage.Core.Json.SchemaProviders;

namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Aggregated parameter data across multiple families for CSV output.
/// </summary>
public class AggregatedParamData(ParamSnapshot param) {
    public readonly string DataTypeId = param.DataType.TypeId;
    public readonly bool HasValueForAllTypes = param.HasValueForAllTypes();
    public readonly bool IsBuiltIn = param.IsBuiltIn;
    public readonly bool IsInstance = param.IsInstance;
    public readonly bool IsProjectParameter = param.IsProjectParameter;
    public readonly string ParamName = param.Name;
    public readonly string SharedGuid = param.SharedGuid.ToString();
    public readonly string StorageType = param.StorageType.ToString();

    public string DataType = SpecNamesProvider.GetLabelMap()
        .ToDictionary(kvp => kvp.Value, kvp => kvp.Key)
        .TryGetValue(param.DataType, out var label)
        ? label
        : string.Empty;

    public int FamilyCount => this.FamilyNames.Count;
    public int ScheduleCount => this.ScheduleNames.Count;
    public bool IsFamilyParameter => string.IsNullOrWhiteSpace(this.SharedGuid) && !this.IsBuiltIn;
    public HashSet<string> ScheduleCategories { get; set; } = [];
    public List<string> ScheduleNames { get; set; } = [];
    public HashSet<string> FamilyCategories { get; set; } = [];
    public List<string> FamilyNames { get; set; } = [];
}