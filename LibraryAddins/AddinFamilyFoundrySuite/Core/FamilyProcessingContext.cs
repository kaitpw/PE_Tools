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
    ///     Checks if a parameter has a non-empty value for all family types.
    /// </summary>
    public bool ParamHasValueForAllTypes(string paramName) =>
        HasValueForAllTypes(this.FindParam(paramName));

    /// <summary>
    ///     Gets the count of types that have a value for the specified parameter.
    /// </summary>
    public int GetTypesWithValue(string paramName) =>
        GetTypesWithValue(this.FindParam(paramName));

    /// <summary>
    ///     Finds a parameter in the pre-process snapshot by name.
    /// </summary>
    public ParamSnapshot? FindParam(string paramName) {
        var parameters = this.PreProcessSnapshot?.Parameters;
        if (parameters is null || parameters.Count == 0)
            return null;

        // There can be both instance+type params with the same name; pick the one with most populated data.
        return parameters
            .Where(p => string.Equals(p.Name, paramName, StringComparison.Ordinal))
            .OrderByDescending(p => p.ValuesPerType.Values.Count(v => !string.IsNullOrWhiteSpace(v)))
            .FirstOrDefault();
    }

    private static bool HasValueForAllTypes(ParamSnapshot? p) {
        if (p is null)
            return false;

        if (!string.IsNullOrWhiteSpace(p.Formula))
            return true;

        if (p.ValuesPerType.Count == 0)
            return false;

        return p.ValuesPerType.Values.All(v => !string.IsNullOrWhiteSpace(v));
    }

    private static int GetTypesWithValue(ParamSnapshot? p) {
        if (p is null)
            return 0;

        if (!string.IsNullOrWhiteSpace(p.Formula))
            return p.ValuesPerType.Count;

        return p.ValuesPerType.Values.Count(v => !string.IsNullOrWhiteSpace(v));
    }
}


