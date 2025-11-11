using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PeExtensions.FamDocument;
using System.ComponentModel;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class SortParams(SortParamsSettings settings) : DocOperation<SortParamsSettings>(settings) {
    public ParamTypeSortOrder TypeSort { get; init; } = settings.ParamTypeSortOrder;
    public ParamValueSortOrder ValueSort { get; init; } = settings.ParamValueSortOrder;
    public ParamNameSortOrder NameSort { get; init; } = settings.ParamNameSortOrder;

    public override string Description =>
        $"Sort family parameters ({this.NameSort}, {this.TypeSort}, {this.ValueSort})";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        var order = this.NameSort == ParamNameSortOrder.Ascending
            ? ParametersOrder.Ascending
            : ParametersOrder.Descending;
        doc.FamilyManager.SortParameters(order);

        var parameters = doc.FamilyManager.GetParameters();
        var baseSort = parameters
            .OrderBy(p => this.TypeSort == ParamTypeSortOrder.FamilyParamsFirst ? p.IsShared : !p.IsShared)
            .ThenBy(p => this.ValueSort == ParamValueSortOrder.ValuesFirst
                ? p.IsDeterminedByFormula
                : !p.IsDeterminedByFormula);

        var sortedParams = (this.NameSort == ParamNameSortOrder.Ascending
                ? baseSort.ThenBy(p => p.Definition.Name, StringComparer.Ordinal)
                : baseSort.ThenByDescending(p => p.Definition.Name, StringComparer.Ordinal))
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
    SharedParamsFirst,
    FamilyParamsFirst
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ParamValueSortOrder {
    FormulasFirst,
    ValuesFirst
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ParamNameSortOrder {
    Ascending,
    Descending
}

public class SortParamsSettings : IOperationSettings {
    [Description("Sort shared parameters first or family parameters first. Takes first priority")]
    public ParamTypeSortOrder ParamTypeSortOrder { get; init; } = ParamTypeSortOrder.SharedParamsFirst;

    [Description("Sort parameters with formulas first or values first. Takes second priority")]
    public ParamValueSortOrder ParamValueSortOrder { get; init; } = ParamValueSortOrder.ValuesFirst;

    [Description("Sort parameters alphabetically. Takes third priority")]
    public ParamNameSortOrder ParamNameSortOrder { get; init; } = ParamNameSortOrder.Ascending;

    public bool Enabled { get; init; } = true;
}