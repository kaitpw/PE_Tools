using AddinFamilyFoundrySuite.Core.Operations.Settings;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteUnusedParams : IOperation<DeleteUnusedParamsSettings> {
    public DeleteUnusedParams(List<string> ExcludeNamesEqualing) =>
        this.ExternalExcludeNamesEqualing = ExcludeNamesEqualing;

    public List<string> ExternalExcludeNamesEqualing { get; set; } = [];
    public DeleteUnusedParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;

    public string Description => "Recursively delete unused parameters from the family"; 

    public OperationLog Execute(Document doc) {
        var log = new OperationLog(this.GetType().Name);
        this.RecursiveDelete(doc, log);
        return log;
    }

    private void RecursiveDelete(Document doc, OperationLog log) {
        var deleteCount = 0;

        var parameters = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => !this.ExternalExcludeNamesEqualing.Contains(p.Definition.Name))
            .Where(p => !ParameterUtils.IsBuiltInParameter(p.Id))
            .Where(this.Settings.Filter)
            .OrderByDescending(p => p.Formula?.Length ?? 0)
            .ToList();

        foreach (var param in parameters) {
            if (param.AssociatedParameters.Cast<Parameter>().Any()) continue;
            if (param.AssociatedArrays(doc).Any()) continue;
            if (param.AssociatedDimensions(doc).Any()) continue;
            if (param.AssociatedFamilyParameters(doc, excludeUnused: true).Any()) continue;

            try {
                var paramName = param.Definition.Name;
                doc.FamilyManager.RemoveParameter(param);
                log.Entries.Add(new LogEntry { Item = paramName });
                deleteCount++;
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry { Item = param.Definition.Name, Error = ex.Message });
            }
        }

        if (deleteCount > 0) this.RecursiveDelete(doc, log);
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