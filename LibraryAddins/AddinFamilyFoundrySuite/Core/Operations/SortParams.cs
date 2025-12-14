using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PeExtensions.FamDocument;
using System.ComponentModel;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class SortParams(SortParamsSettings settings) : DocOperation<SortParamsSettings>(settings) {
    public override string Description =>
        $"Sort family parameters ({this.Settings.ParamNameSortOrder}, {this.Settings.ParamTypeSortOrder}, {this.Settings.ParamValueSortOrder})";

    public IComparer<string> GetNameComparer() {
        var order = this.Settings.ParamNameSortOrder;
        return order switch {
            // ParamNameSortOrder.None => null,
            ParamNameSortOrder.Ascending => StringComparer.Ordinal,
            ParamNameSortOrder.Descending =>
                Comparer<string>.Create((a, b) => StringComparer.Ordinal.Compare(b, a)),
            _ => throw new ArgumentException($"Invalid param name sort order: {order}")
        };
    }

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        var parameters = doc.FamilyManager.GetParameters();

        var sortedParams = parameters
            .OrderByDescending(p => this.Settings.ParamTypeSortOrder == ParamTypeSortOrder.SharedParamsFirst
                ? p.IsShared
                : !p.IsShared)
            .ThenByDescending(p => this.Settings.ParamValueSortOrder == ParamValueSortOrder.FormulasFirst
                ? p.IsDeterminedByFormula
                : !p.IsDeterminedByFormula)
            .ThenBy(p => p.Definition.Name, this.GetNameComparer())
            .ToList();

        foreach (var p in sortedParams)
            Debug.WriteLine($"{p.Definition.Name} {string.IsNullOrWhiteSpace(p.Formula)} {p.IsDeterminedByFormula}");

        doc.FamilyManager.ReorderParameters(sortedParams);

        logs.Add(new LogEntry { Item = $"Sorted {parameters.Count} parameters" });
        return new OperationLog(this.Name, logs);
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ParamTypeSortOrder {
    // None,
    SharedParamsFirst,
    FamilyParamsFirst
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ParamValueSortOrder {
    // None,
    FormulasFirst,
    ValuesFirst
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ParamNameSortOrder {
    // None,
    Ascending,
    Descending
}

public class SortParamsSettings : IOperationSettings {
    [Description(
        "Sort shared parameters first or family parameters first. Takes first priority. Options are SharedParamsFirst or FamilyParamsFirst")]
    public ParamTypeSortOrder ParamTypeSortOrder { get; init; } = ParamTypeSortOrder.SharedParamsFirst;

    [Description(
        "Sort parameters with formulas first or values first. Takes second priority. Options are FormulasFirst or ValuesFirst")]
    public ParamValueSortOrder ParamValueSortOrder { get; init; } = ParamValueSortOrder.ValuesFirst;

    [Description("Sort parameters alphabetically. Takes third priority. Options are Ascending or Descending")]
    public ParamNameSortOrder ParamNameSortOrder { get; init; } = ParamNameSortOrder.Ascending;

    public bool Enabled { get; init; } = true;
}