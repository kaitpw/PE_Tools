using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapReplaceParams : DocOperation<RuntimeMapParamsSettings> {
    private readonly
        Dictionary<string, (ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>
        _sharedParamsDict;

    public MapReplaceParams(
        RuntimeMapParamsSettings runtimeSettings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : base(runtimeSettings) => this._sharedParamsDict = sharedParams.ToDictionary(p => p.externalDefinition.Name);

    public MapReplaceParams(
        MapParamsSettings settings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : this(new RuntimeMapParamsSettings(settings.MappingData), sharedParams) { }

    public override string Description => "Replace a family's existing parameters with APS shared parameters";

    public override OperationLog Execute(FamilyDocument doc) {
        this.Settings.Reset();
        var logs = new List<LogEntry>();
        var fm = doc.FamilyManager;

        foreach (var mapping in this.Settings.UnProcessedMappingData) {
            if (!this._sharedParamsDict.TryGetValue(mapping.NewName, out var sharedParam)) {
                logs.Add(new LogEntry { Item = mapping.NewName, Error = "APS parameter not found in cache" });
                continue;
            }

            try {
                var currentParam = fm.FindParameter(mapping.CurrName);
                if (currentParam == null) continue;
                if (ParameterUtils.IsBuiltInParameter(currentParam.Id)) {
                    continue;
                }

                if (currentParam.Definition.GetDataType() != sharedParam.externalDefinition.GetDataType()) continue;

                var replaced = fm.ReplaceParameter(
                    currentParam,
                    sharedParam.externalDefinition,
                    sharedParam.groupTypeId,
                    sharedParam.isInstance
                );
                this.Settings.MarkNewNameAsProcessed(mapping.NewName);
                logs.Add(new LogEntry { Item = $"{mapping.CurrName} → {replaced.Definition.Name}" });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = mapping.NewName, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}