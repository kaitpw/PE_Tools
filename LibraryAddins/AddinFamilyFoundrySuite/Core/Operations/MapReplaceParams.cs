using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapReplaceParams : IOperation<MapParamsSettings> {
    private readonly Dictionary<string, (ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> _sharedParamsDict;

    public MapReplaceParams(
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) => this._sharedParamsDict = sharedParams.ToDictionary(p => p.externalDefinition.Name);

    public MapParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc; public string Name { get; set; }

    public string Description => "Replace a family's existing parameters with APS shared parameters";

    public OperationLog Execute(Document doc) {
        var logs = new List<LogEntry>();
        var fm = doc.FamilyManager;

        foreach (var mapping in this.Settings.MappingData) {
            if (!this._sharedParamsDict.TryGetValue(mapping.NewName, out var sharedParam)) {
                logs.Add(new LogEntry { Item = mapping.NewName, Error = "APS parameter not found in cache" });
                continue;
            }

            try {
                var currentParam = fm.FindParameter(mapping.CurrName);
                if (currentParam == null) continue; // skip silently, errors will show in MapParams operation
                if (ParameterUtils.IsBuiltInParameter(currentParam.Id)) continue;

                if (currentParam.Definition.GetDataType() != sharedParam.externalDefinition.GetDataType()) continue;

                var replaced = fm.ReplaceParameter(
                    currentParam,
                    sharedParam.externalDefinition,
                    sharedParam.groupTypeId,
                    sharedParam.isInstance
                );
                this.Settings.MappingData.First(m => m.NewName == mapping.NewName).isProcessed = true;
                logs.Add(new LogEntry { Item = $"{mapping.CurrName} → {replaced.Definition.Name}" });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = mapping.NewName, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}