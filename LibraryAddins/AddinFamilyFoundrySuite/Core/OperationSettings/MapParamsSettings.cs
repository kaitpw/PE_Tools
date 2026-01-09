using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using PeExtensions.FamDocument.SetValue;
using PeExtensions.FamManager;
using PeServices.Storage.Core.Json.SchemaProcessors;
using PeServices.Storage.Core.Json.SchemaProviders;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.OperationSettings;

public class MapParamsSettings : IOperationSettings {
    [Description("List of parameter remapping rules")]
    [Required]
    public List<MappingData> MappingData { get; init; } = [];

    [Description("Disable per-type fallback to speed up processing. Do not use outside of testing")]
    public bool DisablePerTypeFallback { get; init; } = false;

    public bool Enabled { get; init; } = true;

    /// <summary>
    ///     Returns current parameters from <paramref name="currNames" /> ranked by data quality and user priority.
    ///     Filters to parameters in FamilyManager with snapshot data, deduplicates by value signature (keeping highest
    ///     priority), then ranks by number of types with values (most first), using user order as tiebreaker.
    /// </summary>
    /// <remarks>
    ///     In an attempt to keep user priority, this DOES NOT rank by matching datatype.
    ///     You must check datatype equality manually whereever that is a necessary condition.
    /// </remarks>
    /// <param name="currNames">Ordered list of candidate parameter names (priority order)</param>
    /// <param name="fm">FamilyManager instance for resolving parameters</param>
    /// <param name="processingContext">Optional context for snapshot data and value counts; may be null</param>
    /// <returns>FamilyParameters ranked by data quality (most populated types first) and user priority</returns>
    public List<FamilyParameter> GetRankedCurrParams(
        List<string> currNames,
        FamilyManager fm,
        FamilyProcessingContext processingContext = null) {
        // No context? Return params in user priority order
        if (processingContext == null) {
            return [.. currNames
                    .Select(fm.FindParameter)
                    .Where(p => p is not null)
                    .Where(p => {
                        // Edge-case: param.Definition throws a null reference exception
                        try {
                            return !string.IsNullOrWhiteSpace(p.Definition.Name);
                        } catch {
                            return false;
                        }
                    })];
        }

        // 1. Filter to params that currently exist in fm AND have snapshots (for quality metrics)
        // 2. Group by value signature (all types, including empty)
        // 3. For each unique value set, take first by user priority
        var candidateSnapshots = currNames
            .Select(processingContext.FindParam)
            .Where(x => x != null)
            .Where(x => x.GetTypesWithValue().Count > 0);

        if (!candidateSnapshots.Any()) return [];

        var deduplicated = candidateSnapshots
            .GroupBy(GetValueSignature)
            .Select(g => g.First());

        if (!deduplicated.Any()) return [];


        // 4. Order by quality (most types with values first). exclude 
        return [
            .. deduplicated
                .Select(x => (n: x.Name, c: x.GetTypesWithValue().Count))
                .OrderByDescending(x => x.c)
                .ThenBy(x => currNames.IndexOf(x.n)) // preserve user priority as tiebreaker
                .Select(x => fm.FindParameter(x.n))
                .Where(p => p is not null)
                .Where(p => {
                    // Edge-case: param.Definition throws a null reference exception
                    try {
                        return !string.IsNullOrWhiteSpace(p.Definition.Name);
                    } catch {
                        return false;
                    }
                    })
        ];
    }

    private static string GetValueSignature(ParamSnapshot snapshot) {
        // Create stable signature from all type values (sorted by type name for consistency)
        // For parameters with formulas, include the formula in the signature
        if (!string.IsNullOrWhiteSpace(snapshot.Formula)) return $"FORMULA:{snapshot.Formula}";

        var sortedValues = snapshot.ValuesPerType
            .OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}:{kv.Value ?? "NULL"}");
        return string.Join("|", sortedValues);
    }
}

public class MappingData {
    [Description("Current parameter names to map from (ordered by priority)")]
    [Required]
    public List<string> CurrNames { get; set; } = [];

    [Description("New parameter name to map to")]
    [Required]
    [SchemaExamples(typeof(SharedParameterNamesProvider))]
    public string NewName { get; init; }

    [Description(
        "Coercion strategy to use for the remapping. CoerceByStorageType will be used when none is specified.")]
    public ParamCoercionStrategy MappingStrategy { get; init; } = ParamCoercionStrategy.CoerceByStorageType;
}