using AddinFamilyFoundrySuite.Core.OperationSettings;

namespace AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;

public static class ParamSnapshotCompiler {
    /// <summary>
    /// Convert snapshot to FamilyParamModel (definition-only).
    /// </summary>
    public static FamilyParamModel ToFamilyParamModel(this ParamSnapshot p) =>
        new() {
            Name = p.Name,
            PropertiesGroup = p.PropertiesGroup,
            DataType = p.DataType,
            IsInstance = p.IsInstance,
            Formula = p.Formula
        };

    /// <summary>
    /// Extract global assignments (formulas or uniform values across all types).
    /// </summary>
    public static IEnumerable<SetParamModel> ToGlobalAssignments(this IEnumerable<ParamSnapshot> snapshots) {
        foreach (var p in snapshots.Where(s => !s.IsBuiltIn)) {
            if (!string.IsNullOrWhiteSpace(p.Formula)) {
                yield return new SetParamModel {
                    Name = p.Name,
                    DataType = p.DataType,
                    IsInstance = p.IsInstance,
                    PropertiesGroup = p.PropertiesGroup,
                    ValueOrFormula = p.Formula,
                    SetAsFormula = true
                };
                continue;
            }

            var distinct = p.ValuesPerType.Values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();

            if (distinct.Count != 1)
                continue;

            yield return new SetParamModel {
                Name = p.Name,
                DataType = p.DataType,
                IsInstance = p.IsInstance,
                PropertiesGroup = p.PropertiesGroup,
                ValueOrFormula = distinct[0],
                SetAsFormula = false
            };
        }
    }

    /// <summary>
    /// Extract per-type assignments (different values per type).
    /// </summary>
    public static IEnumerable<SetParamPerTypeModel> ToPerTypeAssignments(this IEnumerable<ParamSnapshot> snapshots) {
        foreach (var p in snapshots.Where(s => !s.IsBuiltIn)) {
            if (!string.IsNullOrWhiteSpace(p.Formula))
                continue;

            var distinct = p.ValuesPerType.Values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();

            if (distinct.Count <= 1)
                continue;

            yield return new SetParamPerTypeModel {
                Name = p.Name,
                DataType = p.DataType,
                IsInstance = p.IsInstance,
                PropertiesGroup = p.PropertiesGroup,
                ValuesPertype = p.ValuesPerType
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                    .ToDictionary(kv => kv.Key, kv => kv.Value!)
            };
        }
    }

    /// <summary>
    /// Compile full snapshot list into AddAndSetParamsSettings.
    /// </summary>
    public static AddAndSetParamsSettings ToAddAndSetSettings(this IEnumerable<ParamSnapshot> snapshots) => new() {
        Parameters = snapshots.ToGlobalAssignments().ToList(),
        ParametersPerType = snapshots.ToPerTypeAssignments().ToList()
    };
}
