using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteUnusedParamsSettings {
    [Required] public List<string> ExcludeNamesEqualing { get; init; } = [];
    [Required] public List<string> ExcludeNamesContaining { get; init; } = [];
    [Required] public List<string> ExcludeNamesStartingWith { get; init; } = [];


    public bool Filter(FamilyParameter p) =>
        Exclude(this.ExcludeNamesEqualing, p.Definition.Name.Equals)
        && Exclude(this.ExcludeNamesContaining, p.Definition.Name.Contains)
        && Exclude(this.ExcludeNamesStartingWith, p.Definition.Name.StartsWith);
    private static bool Exclude<T>(List<T> list, Func<T, bool> predicate) =>
        list.Count == 0 || !list.Any(predicate); // Pass if empty OR condition NOT met
}
public class DeleteUnusedParamsOperation : IOperation<DeleteUnusedParamsSettings> {
    public DeleteUnusedParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Delete Unused Parameters";
    public string Description => "Recursively delete unused parameters from the family";

    public void Execute(Document doc) =>
        this.RecursiveDelete(doc, new List<List<string>>());

    private List<List<string>> RecursiveDelete(Document doc, List<List<string>> results) {
        var deleteCount = 0;

        var parameters = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(this.Settings.Filter)
            .OrderByDescending(p => p.Formula?.Length ?? 0)
            .ToList();

        var iterationResults = new List<string>();

        foreach (var param in parameters) {
            if (param.AssociatedParameters.Cast<Parameter>().Any()) continue;
            if (param.AssociatedArrays(doc).Any()) continue;
            if (param.AssociatedDimensions(doc).Any()) continue;
            if (param.AssociatedFamilyParameters(doc).Any()) continue;

            try {
                var paramName = param.Definition.Name;
                doc.FamilyManager.RemoveParameter(param);
                iterationResults.Add(paramName);
                deleteCount++;
            } catch { }
        }

        results.Add(iterationResults);

        return deleteCount > 0
            ? this.RecursiveDelete(doc, results)
            : results;
    }
}