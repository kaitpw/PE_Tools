using AddinFamilyFoundrySuite.Core.OperationGroups;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Copies parameter values from source params to target params for the current family type.
///     Iterates through CurrNames in priority order, using the first match found.
/// </summary>
public class MapParams(MapParamsSettings settings, MapParamsSharedState sharedState = null)
    : TypeOperation<MapParamsSettings>(settings) {
    public override string Description => "Map an old parameter's value to a new parameter for each family type";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        var mappingsToProcess = sharedState?.GetCurrentMappings() ?? this.Settings.MappingData;
        var fm = doc.FamilyManager;

        foreach (var mapping in mappingsToProcess.Where(m => !m.IsProcessed)) {
            var tgtParam = fm.FindParameter(mapping.NewName);
            if (tgtParam == null) continue;

            // Try each CurrName in priority order until one succeeds
            foreach (var currName in mapping.CurrNames) {
                var srcParam = fm.FindParameter(currName);
                if (srcParam == null) continue;

                var mappingDesc = $"{currName} → {mapping.NewName}";
                try {
                    if (tgtParam.Formula != null) _ = doc.UnsetFormula(tgtParam);

                    _ = doc.SetValue(tgtParam, srcParam, mapping.MappingStrategy);
                    logs.Add(tgtParam != srcParam
                        ? new LogEntry { Item = $"Coerced {mappingDesc} using {mapping.MappingStrategy}" }
                        : new LogEntry { Item = $"Set {mappingDesc}" });
                    break; // Success - skip remaining CurrNames
                } catch (Exception ex) {
                    logs.Add(new LogEntry { Item = mappingDesc, Error = ex.Message });
                }
            }
        }

        return new OperationLog(this.Name, logs);
    }
}
