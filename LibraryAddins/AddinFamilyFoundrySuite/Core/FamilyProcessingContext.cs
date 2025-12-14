using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;

namespace AddinFamilyFoundrySuite.Core;

/// <summary>
///     Context for a single family's processing run, containing pre/post snapshots and operation logs.
/// </summary>
public class FamilyProcessingContext {
    public required string FamilyName { get; init; }

    /// <summary>
    ///     Snapshot collected before processing.
    /// </summary>
    public FamilySnapshot? PreProcessSnapshot { get; set; }

    /// <summary>
    ///     Snapshot collected after processing.
    /// </summary>
    public FamilySnapshot? PostProcessSnapshot { get; set; }

    /// <summary>
    ///     Operation logs from processing, or an error if processing failed.
    /// </summary>
    public Result<List<OperationLog>> OperationLogs { get; set; }

    /// <summary>
    ///     Total processing time in milliseconds.
    /// </summary>
    public double TotalMs { get; set; }


    /// <summary>
    ///     Finds a parameter in the pre-process snapshot by name.
    /// </summary>
    public ParamSnapshot FindParam(string paramName) {
        var parameters = this.PreProcessSnapshot?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return null;

        return parameters
            .Where(p => string.Equals(p.Name, paramName, StringComparison.Ordinal))
            .OrderByDescending(this.CountTypesWithValue)
            .FirstOrDefault();
    }

    /// <summary>
    ///     Checks if a parameter has a (non-empty) value for all family types.
    /// </summary>
    public bool HasValueForAllTypes(string paramName) =>
        this.HasValueForAllTypes(this.FindParam(paramName));

    /// <summary>
    ///     Checks if a parameter has a (non-empty) value for all family types.
    /// </summary>
    public bool HasValueForAllTypes(ParamSnapshot p) {
        if (p is null) return false;
        var familyTypes = p.ValuesPerType.Count;
        if (familyTypes == 0) return false;
        return familyTypes == this.CountTypesWithValue(p);
    }

    /// <summary>
    ///     Gets the count of types that have a value for the specified parameter.
    /// </summary>
    public int CountTypesWithValue(string paramName) =>
        this.CountTypesWithValue(this.FindParam(paramName));

    /// <summary>
    ///     Gets the count of types that have a value for the specified parameter.
    /// </summary>
    public int CountTypesWithValue(ParamSnapshot p) => this.GetTypesWithValue(p).Count;

    /// <summary>
    ///     Gets the list of family types that have a value for the specified parameter.
    /// </summary>
    public List<string> GetTypesWithValue(string paramName) =>
        this.GetTypesWithValue(this.FindParam(paramName));

    /// <summary>
    ///     Gets the list of family types that have a value for the specified parameter.
    /// </summary>

    public List<string> GetTypesWithValue(ParamSnapshot p) {
        if (p is null
            || this.PreProcessSnapshot?.Parameters == null
            || this.PreProcessSnapshot.Parameters.Count == 0) {
            return [];
        }

        return string.IsNullOrWhiteSpace(p.Formula)
            ? p.ValuesPerType
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => kv.Key)
                .ToList()
            : p.ValuesPerType.Keys.ToList();
    }
}