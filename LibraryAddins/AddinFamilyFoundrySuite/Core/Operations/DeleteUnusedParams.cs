using AddinFamilyFoundrySuite.Core.Operations.Settings;
using System.ComponentModel.DataAnnotations;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteUnusedParams : IOperation<DeleteUnusedParamsSettings> {
    public DeleteUnusedParams(List<string> ExcludeNamesEqualing) =>
        this.ExternalExcludeNamesEqualing = ExcludeNamesEqualing;

    public List<string> ExternalExcludeNamesEqualing { get; set; } = [];
    public DeleteUnusedParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Delete Unused Parameters";
    public string Description => "Recursively delete unused parameters from the family";

    public void Execute(Document doc) => this.RecursiveDelete(doc, new List<List<string>>());

    private List<List<string>> RecursiveDelete(Document doc, List<List<string>> results) {
        var deleteCount = 0;

        var parameters = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => !this.ExternalExcludeNamesEqualing.Contains(p.Definition.Name))
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

    private void LogDeletionSummary(List<List<string>> results) {
        Debug.WriteLine("\n=== DELETION SUMMARY ===");
        var totalDeleted = results.Sum(r => r.Count);
        Debug.WriteLine($"Total iterations: {results.Count}");
        Debug.WriteLine($"Total parameters deleted: {totalDeleted}");

        for (var i = 0; i < results.Count; i++) {
            var iterationResults = results[i];
            Debug.WriteLine($"\nIteration {i + 1}:");
            Debug.WriteLine($"  Parameters deleted: {iterationResults.Count}");
            if (iterationResults.Any()) {
                Debug.WriteLine("  Deleted parameters:");
                foreach (var param in iterationResults) Debug.WriteLine($"    - {param}");
            }
        }
    }
}

public class DeleteUnusedParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
    [Required] public Exclude ExcludeNames { get; init; } = new();

    public bool Filter(FamilyParameter p) => !this.IsExcluded(p);

    private bool IsExcluded(FamilyParameter p) =>
        this.ExcludeNames.Equaling.Any(p.Definition.Name.Equals) ||
        this.ExcludeNames.Containing.Any(p.Definition.Name.Contains) ||
        this.ExcludeNames.StartingWith.Any(p.Definition.Name.StartsWith);
}