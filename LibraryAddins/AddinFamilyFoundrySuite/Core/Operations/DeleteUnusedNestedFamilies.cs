namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteUnusedNestedFamilies : IOperation<DeleteUnusedNestedFamiliesSettings> {
    public DeleteUnusedNestedFamiliesSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Description => "Delete unused nested families from the family";

    public OperationLog Execute(Document doc) {
        var logs = new List<LogEntry>();

        var allFamilies = new FilteredElementCollector(doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.Name != "")
            .Where(f => f.FamilyCategory?.BuiltInCategory != BuiltInCategory.OST_LevelHeads)
            .Where(f => f.FamilyCategory?.BuiltInCategory != BuiltInCategory.OST_SectionHeads)
            .ToList();
        if (allFamilies.Count == 0) return new OperationLog(((IOperation)this).Name, logs);

        var usedFamilyNames = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(fi => fi.Symbol?.Family != null)
            .Select(fi => fi.Symbol.Family.Name)
            .ToHashSet();

        var unusedFamilies = allFamilies.Where(f => !usedFamilyNames.Contains(f.Name)).ToList();
        if (unusedFamilies.Count == 0) return new OperationLog(((IOperation)this).Name, logs);

        foreach (var family in unusedFamilies) {
            var familyName = family.Name?.Trim() ?? "";
            try {
                var dependentCount = family.GetDependentElements(null).Count;
                if (dependentCount > 100) continue; // skip anomalies

                _ = doc.Delete(family.Id);
                logs.Add(new LogEntry { Item = familyName });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = familyName, Error = ex.Message });
            }
        }

        return new OperationLog(((IOperation)this).Name, logs);
    }
}

public class DeleteUnusedNestedFamiliesSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
}