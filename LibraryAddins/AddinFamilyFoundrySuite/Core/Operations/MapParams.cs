using AddinFamilyFoundrySuite.Core.OperationSettings;
using AddinFamilyFoundrySuite.Core.OperationGroups;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapParams : TypeOperation<MapParamsSettings> {
    private readonly MapParamsSharedState _sharedState;

    public MapParams(MapParamsSettings settings, MapParamsSharedState sharedState = null)
        : base(settings) {
        this._sharedState = sharedState;
    }

    public override string Description => "Map an old parameter's value to a new parameter for each family type";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        // Get current mappings (either fresh or modified by MapReplaceParams)
        var mappingsToProcess = this._sharedState?.GetCurrentMappings() ?? this.Settings.MappingData;

        foreach (var mapping in mappingsToProcess.Where(m => !m.IsProcessed)) {
            // Try each CurrName in priority order until one succeeds
            var foundMatch = false;
            foreach (var currName in mapping.CurrName) {
                if (foundMatch) break;

                var mappingDesc = $"{currName} → {mapping.NewName}";

                try {
                    var sourceParam = doc.FamilyManager.FindParameter(currName);
                    var targetParam = doc.FamilyManager.FindParameter(mapping.NewName);

                    if (sourceParam is null) continue;
                    if (targetParam is null) {
                        logs.Add(new LogEntry { Item = mappingDesc, Error = $"{mapping.NewName} not found in the family" });
                        continue;
                    }

                    _ = doc.SetValue(targetParam, sourceParam, mapping.MappingStrategy);
                    foundMatch = true;

                    // Backlink: if original source is built-in, set source.Formula = target.Name
                    if (ParameterUtils.IsBuiltInParameter(sourceParam.Id)) {
                        if (sourceParam.IsInstance != targetParam.IsInstance) {
                            logs.Add(new LogEntry {
                                Item = $"Backlink {mapping.NewName} → {currName}",
                                Error = $"Cannot set formulas for mismatching instance/type " +
                                        $"({sourceParam.Name()} is {sourceParam.GetTypeInstanceDesignation()} " +
                                        $"but {targetParam.Name()} is {targetParam.GetTypeInstanceDesignation()})"
                            });
                        } else {
                            doc.SetFormulaNative(sourceParam, targetParam.Definition.Name);
                        }
                    }

                    mapping.IsProcessed = true;
                    logs.Add(new LogEntry { Item = mappingDesc });
                } catch (Exception ex) {
                    logs.Add(new LogEntry { Item = mappingDesc, Error = ex.Message });
                }
            }
        }

        return new OperationLog(this.Name, logs);
    }
}