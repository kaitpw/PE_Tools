using AddinFamilyFoundrySuite.Core.OperationGroups;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapParams(MapParamsSettings settings, MapParamsSharedState sharedState = null)
    : TypeOperation<MapParamsSettings>(settings) {
    public override string Description => "Map an old parameter's value to a new parameter for each family type";

    /// <summary>
    ///     Tracks backlinks that need to be created AFTER all types have been processed.
    ///     Key: source param name, Value: (srcParam, tgtParam) tuple.
    ///     Using shared state ensures backlinks are only created once after all type iterations complete.
    /// </summary>
    private readonly Dictionary<string, (FamilyParameter src, FamilyParameter tgt)> _pendingBacklinks =
        sharedState?.PendingBacklinks ?? new Dictionary<string, (FamilyParameter, FamilyParameter)>();

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        // Get current mappings (either fresh or modified by MapReplaceParams)
        var mappingsToProcess = sharedState?.GetCurrentMappings() ?? this.Settings.MappingData;
        var isLastType = IsLastFamilyType(doc.FamilyManager);

        foreach (var mapping in mappingsToProcess.Where(m => !m.IsProcessed)) {
            foreach (var currName in mapping.CurrNames) {
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

                    // Queue backlink for AFTER all types are processed (prevents breaking source values)
                    var srcName = srcParam.Definition.Name;
                    if (!this._pendingBacklinks.ContainsKey(srcName))
                        this._pendingBacklinks[srcName] = (srcParam, tgtParam);
                } catch (Exception ex) {
                    logs.Add(new LogEntry { Item = mappingDesc, Error = ex.Message });
                }
            }
        }

        // Execute all pending backlinks only on the last type iteration
        if (isLastType) {
            foreach (var (srcName, (srcParam, tgtParam)) in this._pendingBacklinks) {
                var backlinkLog = Backlink(doc, srcParam, tgtParam);
                if (backlinkLog is not null) logs.Add(backlinkLog);
            }

            this._pendingBacklinks.Clear();
        }

        return new OperationLog(this.Name, logs);
    }

    private static bool IsLastFamilyType(FamilyManager fm) {
        var types = fm.Types.Cast<FamilyType>().ToList();
        if (types.Count == 0) return true;
        var currentName = fm.CurrentType?.Name;
        return currentName != null && currentName == types.Last().Name;
    }

    public static LogEntry Backlink(FamilyDocument doc, FamilyParameter srcParam, FamilyParameter tgtParam) {
        var srcName = srcParam.Definition.Name;
        var tgtName = tgtParam.Definition.Name;
        if (ParameterUtils.IsBuiltInParameter(srcParam.Id)) {
            if (tgtParam.Formula != tgtName) {
                var success = doc.TrySetFormulaFast(srcParam, tgtName, out var errorMessage);
                if (!success) return new LogEntry { Item = $"Backlink {tgtName} → {srcName}", Error = errorMessage };

                return new LogEntry { Item = $"Backlink {tgtName} → {srcName}" };
            }
        }

        return null;
    }
}