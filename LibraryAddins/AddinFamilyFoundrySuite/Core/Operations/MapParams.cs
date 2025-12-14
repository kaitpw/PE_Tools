using AddinFamilyFoundrySuite.Core.OperationGroups;
using AddinFamilyFoundrySuite.Core.OperationSettings;
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
            var prioritizedNames = this.PrioritizeCurrNames(mapping.CurrNames);

            // Try each CurrName in priority order until one succeeds
            var foundMatch = false;
            foreach (var currName in prioritizedNames) {
                if (foundMatch) break;

                var mappingDesc = $"{currName} → {mapping.NewName}";

                try {
                    var srcParam = doc.FamilyManager.FindParameter(currName);
                    var tgtParam = doc.FamilyManager.FindParameter(mapping.NewName);

                    if (srcParam is null) continue;
                    if (tgtParam is null) {
                        logs.Add(new LogEntry {
                            Item = mappingDesc,
                            Error = $"{mapping.NewName} not found in the family"
                        });
                        continue;
                    }

                    if (tgtParam.Formula != null) _ = doc.UnsetFormula(tgtParam);

                    _ = doc.SetValue(tgtParam, srcParam, mapping.MappingStrategy);
                    if (tgtParam != srcParam)
                        logs.Add(new LogEntry { Item = $"Coerced {mappingDesc} using {mapping.MappingStrategy}" });
                    else
                        logs.Add(new LogEntry { Item = $"Set {mappingDesc}" });
                    var backlinkLog = Backlink(doc, srcParam, tgtParam);
                    if (backlinkLog is not null) logs.Add(backlinkLog);
                    foundMatch = true;
                } catch (Exception ex) {
                    logs.Add(new LogEntry { Item = mappingDesc, Error = ex.Message });
                }
            }
        }

        return new OperationLog(this.Name, logs);
    }

    public static LogEntry Backlink(FamilyDocument doc, FamilyParameter srcParam, FamilyParameter tgtParam) {
        var srcName = srcParam.Definition.Name;
        var tgtName = tgtParam.Definition.Name;
        if (ParameterUtils.IsBuiltInParameter(srcParam.Id)) {
            if (tgtParam.Formula is null) {
                var success = doc.SetFormulaFast(srcParam, tgtName, out var errorMessage);
                if (!success) return new LogEntry { Item = $"Backlink {tgtName} → {srcName}", Error = errorMessage };

                return new LogEntry { Item = $"Backlink {tgtName} → {srcName}" };
            }
        }

        return null;
    }

    /// <summary>
    ///     Prioritizes CurrName options by which have values for all types.
    ///     Falls back to original order when snapshot is not available.
    /// </summary>
    /// <remarks>
    ///     Does not prioritize by number of types with values. while technically a good idea
    ///     it may add excessive complexity to the operation, making it harder for users to understand.
    /// </remarks>
    private IEnumerable<string> PrioritizeCurrNames(List<string> currNames) {
        if (this._context?.PreProcessSnapshot?.Parameters == null ||
            this._context.PreProcessSnapshot.Parameters.Count == 0)
            return currNames;

        // Sort by: HasValueForAllTypes first, then original order
        return currNames
            .OrderByDescending(this._context.ParamHasValueForAllTypes)
            .ThenBy(currNames.IndexOf);
    }
}