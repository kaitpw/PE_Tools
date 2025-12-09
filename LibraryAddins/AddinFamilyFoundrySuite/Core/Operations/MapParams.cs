using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapParams : TypeOperation<RuntimeMapParamsSettings> {
    public MapParams(RuntimeMapParamsSettings runtimeSettings) : base(runtimeSettings) { }

    public MapParams(MapParamsSettings settings) : this(new RuntimeMapParamsSettings(settings.MappingData)) { }

    public override string Description => "Map an old parameter's value to a new parameter for each family type";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        foreach (var mapping in this.Settings.UnProcessedMappingData) {
            var mappingDesc = $"{mapping.CurrName} → {mapping.NewName}";

            try {
                var sourceParam = doc.FamilyManager.FindParameter(mapping.CurrName);
                var targetParam = doc.FamilyManager.FindParameter(mapping.NewName);

                if (sourceParam is null || targetParam is null) {
                    var notFoundParam = sourceParam is null ? mapping.CurrName : mapping.NewName;
                    logs.Add(new LogEntry { Item = mappingDesc, Error = $"{notFoundParam} not found in the family" });
                    continue;
                }

                _ = doc.SetValue(targetParam, sourceParam, mapping.MappingStrategy);
                if (ParameterUtils.IsBuiltInParameter(sourceParam.Id)) {
                    if (sourceParam.IsInstance != targetParam.IsInstance) {
                        logs.Add(new LogEntry {
                            Item = $"Backlink {mapping.NewName} → {mapping.CurrName}",
                            Error = $"Cannot set formulas for mismatching instance/type " +
                                    $"({sourceParam.Name()} is {sourceParam.GetTypeInstanceDesignation()} " +
                                    $"but {targetParam.Name()} is {targetParam.GetTypeInstanceDesignation()})"
                        });
                    }

                    doc.FamilyManager.SetFormula(sourceParam, targetParam.Definition.Name);
                }

                this.Settings.MarkNewNameAsProcessed(mapping.NewName);
                logs.Add(new LogEntry { Item = mappingDesc });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = mappingDesc, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}