namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteUnusedParamsOperation : Operation<NoSettings> {
    public override OperationType Type => OperationType.Doc;
    public override string Name => "Delete Unused Parameters";
    public override string Description => "Recursively delete unused parameters from the family";

    protected override void ExecuteCore(Document doc, NoSettings settings) =>
        this.RecursiveDeleteUnusedParameters(doc, new List<List<string>>());

    private List<List<string>> RecursiveDeleteUnusedParameters(Document doc, List<List<string>> results) {
        var deleteCount = 0;

        var parameters = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Where(p => !p.Definition.Name.StartsWith("PE_"))
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
            ? this.RecursiveDeleteUnusedParameters(doc, results)
            : results;
    }
}