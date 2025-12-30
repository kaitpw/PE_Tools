using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

/// <summary>
///     Copies parameter values from source params to target params for the current family type.
///     Iterates through CurrNames in priority order, using the first match found.
/// </summary>
public class MapParams(MapParamsSettings settings)
    : TypeOperation<MapParamsSettings>(settings) {
    public override string Description => "Map an old parameter's value to a new parameter for each family type";

    public override OperationLog Execute(FamilyDocument doc, FamilyProcessingContext processingContext, OperationContext groupContext) {
        var fm = doc.FamilyManager;

        foreach (var mapping in this.Settings.MappingData) {
            var log = groupContext.GetOrCreate(mapping.NewName);
            if (log.IsComplete) continue;  // Previous op fully handled it

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
                    _ = log.Defer(tgtParam != srcParam
                        ? $"Coerced {mappingDesc} using {mapping.MappingStrategy}"
                        : $"Set {mappingDesc}");
                    break; // Success - skip remaining CurrNames
                } catch (Exception ex) {
                    _ = log.Error(mappingDesc, ex);
                }
            }
        }

        return new OperationLog(this.Name, groupContext.TakeSnapshot());
    }
}
