using AddinFamilyFoundrySuite.Core.OperationGroups;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Creates backlinks from built-in parameters to their mapped shared parameter targets.
///     Sets formulas like: Model = PE_G___Model, so the built-in derives from the shared param.
/// </summary>
public class BacklinkParamsToBuiltIn(MapParamsSettings settings, MapParamsSharedState sharedState = null)
    : DocOperation<MapParamsSettings>(settings) {
    public override string Description => "Create backlinks from built-in params to their mapped targets";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        var mappings = sharedState?.GetCurrentMappings() ?? this.Settings.MappingData;
        var fm = doc.FamilyManager;

        foreach (var mapping in mappings) {
            var tgtParam = fm.FindParameter(mapping.NewName);
            if (tgtParam == null) continue;

            // Find first built-in in CurrNames (priority order) and backlink it
            foreach (var currName in mapping.CurrNames) {
                var srcParam = fm.FindParameter(currName);
                if (srcParam == null) continue;
                if (!ParameterUtils.IsBuiltInParameter(srcParam.Id)) continue;

                // Set formula: BuiltIn = NewParam
                var success = doc.TrySetFormulaFast(srcParam, mapping.NewName, out var err);
                logs.Add(success
                    ? new LogEntry { Item = $"Backlink {mapping.NewName} → {currName}" }
                    : new LogEntry { Item = $"Backlink {mapping.NewName} → {currName}", Error = err });
                break; // Only backlink first matching built-in per mapping
            }
        }

        return new OperationLog(this.Name, logs);
    }
}
