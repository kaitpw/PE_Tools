using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamParameter;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class PurgeParams : DocOperation<PurgeParamsSettings>, ISnapshotAwareOperation {
    public override string Description => "Recursively delete unused parameters from the family";
    private FamilyProcessingContext _context;
    public PurgeParams(PurgeParamsSettings settings, IEnumerable<string> ExcludeNamesEqualing) :
        base(settings) =>
        this.ExternalExcludeNamesEqualing = ExcludeNamesEqualing;

    public IEnumerable<string> ExternalExcludeNamesEqualing { get; set; } = [];

    public bool IsOkToDeleteEmptyParam(FamilyParameter param) {
        if (this._context == null) return false;
        if (!this.Settings.DeleteEmptyParameters) return false;

        foreach (var value in this._context.GetTypesWithValue(param.Definition.Name)) {
            if (value == null) return true;
            if (this.Settings.ConsiderZeroValueAsEmpty
                && int.TryParse(value, out var intValue)
                && intValue == 0) return true;
            if (this.Settings.ConsiderEmptyStringAsEmpty
                && string.IsNullOrWhiteSpace(value)) return true;
        }

        return false;
    }

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        this.RecursiveDelete(doc, logs);
        return new OperationLog(this.Name, logs);
    }

    private void RecursiveDelete(FamilyDocument doc, List<LogEntry> logs) {
        var deleteCount = 0;
        var excludeSet = this.ExternalExcludeNamesEqualing.ToHashSet();

        var parameters = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => !excludeSet.Contains(p.Definition.Name))
            .Where(this.Settings.Filter)
            .Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))
            .Where(this.IsOkToDeleteEmptyParam)
            .OrderByDescending(p => p.Formula?.Length ?? 0)
            .ToList();

        foreach (var param in parameters) {
            if (param.HasDirectAssociation(doc)) continue;
            if (param.FormulaDependents(doc).Any(p => p.HasDirectAssociation(doc))) continue;

            try {
                var paramName = param.Definition.Name;
                doc.FamilyManager.RemoveParameter(param);
                logs.Add(new LogEntry { Item = paramName });
                deleteCount++;
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = param.Definition.Name, Error = ex.Message });
            }
        }

        if (deleteCount > 0) this.RecursiveDelete(doc, logs);
    }

    public void SetContext(FamilyProcessingContext context) => this._context = context;

}

public class PurgeParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;

    [Description("Whether to delete parameters that have no value for every family type, regardless of whether they are used in the family. This is rare but possible. This setting is useful for properties like url variations where there are often multiple url parameters with no value.")]
    public bool DeleteEmptyParameters { get; init; } = true;
    [Description("Whether to consider zero value as \"empty\" when deleting empty parameters.")]
    public bool ConsiderZeroValueAsEmpty { get; init; } = true;

    [Description("Whether to consider empty string as \"empty\" when deleting empty parameters.")]
    public bool ConsiderEmptyStringAsEmpty { get; init; } = true;

    [Description("Exclude parameters from the deletion list ")]
    [Required] public Exclude ExcludeNames { get; init; } = new();

    public bool Filter(FamilyParameter p) => !this.IsExcluded(p);

    private bool IsExcluded(FamilyParameter p) =>
        this.ExcludeNames.Equaling.Any(p.Definition.Name.Equals) ||
        this.ExcludeNames.Containing.Any(p.Definition.Name.Contains) ||
        this.ExcludeNames.StartingWith.Any(p.Definition.Name.StartsWith);
}