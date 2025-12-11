using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamParameter;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteUnusedParams : DocOperation<DeleteUnusedParamsSettings> {
    public DeleteUnusedParams(DeleteUnusedParamsSettings settings, IEnumerable<string> ExcludeNamesEqualing) :
        base(settings) =>
        this.ExternalExcludeNamesEqualing = ExcludeNamesEqualing;

    public IEnumerable<string> ExternalExcludeNamesEqualing { get; set; } = [];
    public override string Description => "Recursively delete unused parameters from the family";

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
            .Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))
            .Where(this.Settings.Filter)
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
}

public class DeleteUnusedParamsSettings : IOperationSettings {
    [Required] public Exclude ExcludeNames { get; init; } = new();
    public bool Enabled { get; init; } = true;

    public bool Filter(FamilyParameter p) => !this.IsExcluded(p);

    private bool IsExcluded(FamilyParameter p) =>
        this.ExcludeNames.Equaling.Any(p.Definition.Name.Equals) ||
        this.ExcludeNames.Containing.Any(p.Definition.Name.Contains) ||
        this.ExcludeNames.StartingWith.Any(p.Definition.Name.StartsWith);
}