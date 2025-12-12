using AddinFamilyFoundrySuite.Core.OperationSettings;
using AddinFamilyFoundrySuite.Core.OperationGroups;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapParams : TypeOperation<MapParamsSettings>, ISnapshotAwareOperation {
    private readonly MapParamsSharedState _sharedState;
    private FamilyProcessingContext _context;

    public MapParams(MapParamsSettings settings, MapParamsSharedState sharedState = null)
        : base(settings) => this._sharedState = sharedState;

    public override string Description => "Map an old parameter's value to a new parameter for each family type";

    public void SetContext(FamilyProcessingContext context) => this._context = context;

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        // Get current mappings (either fresh or modified by MapReplaceParams)
        var mappingsToProcess = this._sharedState?.GetCurrentMappings() ?? this.Settings.MappingData;

        foreach (var mapping in mappingsToProcess.Where(m => !m.IsProcessed)) {
            // Prioritize CurrName options by which ones have values for all types (when snapshot available)
            var prioritizedNames = this.PrioritizeCurrNames(mapping.CurrName);

            // Try each CurrName in priority order until one succeeds
            var foundMatch = false;
            foreach (var currName in prioritizedNames) {
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

    /// <summary>
    ///     Prioritizes CurrName options by which have values for all types.
    ///     Falls back to original order when snapshot is not available.
    /// </summary>
    private IEnumerable<string> PrioritizeCurrNames(List<string> currNames) {
        if (this._context?.PreProcessSnapshot?.Parameters == null || this._context.PreProcessSnapshot.Parameters.Count == 0)
            return currNames;

        // Sort by: HasValueForAllTypes first, then TypesWithValue descending, then original order
        return currNames
            .OrderByDescending(this._context.ParamHasValueForAllTypes)
            .ThenByDescending(this._context.GetTypesWithValue)
            .ThenBy(currNames.IndexOf);
    }
}