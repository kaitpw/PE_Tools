namespace AddinFamilyFoundrySuite.Core.Operations;

public static class AddSharedParamsOperation {
    public static List<Result<FamilyParameter>> AddSharedParams(
        Document famDoc,
        List<SharedParameterElement> sharedParams
    ) {
        if (famDoc is null) throw new ArgumentNullException(nameof(famDoc));
        if (sharedParams is null) throw new ArgumentNullException(nameof(sharedParams));
        if (!famDoc.IsFamilyDocument) throw new Exception("Document is not a family document.");
        var fm = famDoc.FamilyManager;
        var results = new List<Result<FamilyParameter>>();

        foreach (var sharedParam in sharedParams) {
            try {
                var externalDefinition = famDoc.Application.OpenSharedParameterFile()?.Groups?
                    .SelectMany(g => g.Definitions)
                    .OfType<ExternalDefinition>()
                    .FirstOrDefault(def => def.GUID == sharedParam.GuidValue);

                if (externalDefinition != null)
                    results.Add(fm.AddParameter(externalDefinition, new ForgeTypeId(""), true));
            } catch (Exception ex) {
                throw new Exception($"Failed to add parameter service parameter {sharedParam.Name}: {ex.Message}");
            }
        }

        return results;
    }
}