using AddinFamilyFoundrySuite.Core.OperationGroups;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;
using PeExtensions.FamParameter.Formula;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapReplaceParams : DocOperation<MapParamsSettings> {
    private readonly
        Dictionary<string, (ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>
        _sharedParamsDict;

    private readonly MapParamsSharedState _sharedState;
    public MapReplaceParams(
        MapParamsSettings settings,
        MapParamsSharedState sharedState,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : base(settings) {
        this._sharedState = sharedState;
        this._sharedParamsDict = sharedParams.ToDictionary(p => p.externalDefinition.Name);
    }

    public override string Description => "Replace a family's existing parameters with APS shared parameters";

    public override OperationLog Execute(FamilyDocument doc) {
        // Create fresh state for THIS family execution
        var mutableMappings = this._sharedState.CreateFreshMappings();
        var logs = new List<LogEntry>();
        var fm = doc.FamilyManager;

        foreach (var mapping in mutableMappings.Where(m => !m.IsProcessed)) {
            if (!this._sharedParamsDict.TryGetValue(mapping.NewName, out var sharedParam)) {
                logs.Add(new LogEntry { Item = mapping.NewName, Error = "APS parameter not found in cache" });
                continue;
            }

            // Try each CurrName in priority order until one succeeds
            var foundMatch = false;
            foreach (var currName in mapping.CurrNames.TakeWhile(_ => !foundMatch)) {
                try {
                    // Validate current parameter exists and is not built-in param. 
                    var currentParam = fm.FindParameter(currName);
                    if (currentParam == null) continue;
                    if (ParameterUtils.IsBuiltInParameter(currentParam.Id)) continue;

                    // Verify that new parameter does not already exist, replacement errors if it does
                    if (fm.FindParameter(mapping.NewName) != null) continue;

                    if (currentParam.Definition.GetDataType() != sharedParam.externalDefinition.GetDataType()) continue;

                    var replaced = fm.ReplaceParameter(
                        currentParam,
                        sharedParam.externalDefinition,
                        sharedParam.groupTypeId,
                        sharedParam.isInstance
                    );

                    foundMatch = true;

                    // Formula contains parameters: No further processing if formula exists
                    // Formula is constant: Unset formula but DO NOT mark as processed, instead set CurrName to NewName 
                    // (CurrName is gone at this point). This allows SetValue to coerce later which is
                    // necessary for electrical parameters using ElectricalCoercionStrategy)
                    var parameters = doc.FamilyManager.Parameters;
                    var singleReference = parameters.TryGetSingleReference(replaced.Formula);
                    if (singleReference != null) {
                        // TODO: experimental, in the spirit of simplification, this is like the Unwrap Operation
                        _ = doc.UnsetFormula(singleReference);
                        mapping.IsProcessed = true;
                        logs.Add(new LogEntry { Item = $"{currName} → {replaced.Definition.Name}" });
                    } else if (replaced.Formula == null ||
                               parameters.IsConstant(replaced.Formula)) {
                        _ = doc.UnsetFormula(replaced);
                        var ignoreCoercion = new List<ForgeTypeId> {
                            SpecTypeId.Number, SpecTypeId.String.Text, SpecTypeId.Length
                        };
                        // skip datatypes that will never need coercion, boosts speed and cleans logs
                        if (ignoreCoercion.Contains(replaced.Definition.GetDataType())) continue;
                        // Update CurrName in LOCAL mutableMappings
                        var mappingToUpdate = mutableMappings.First(m => m.NewName == mapping.NewName);
                        mappingToUpdate.CurrNames = [mapping.NewName];
                        logs.Add(new LogEntry {
                            Item = $"Replaced/waiting to coerce {currName} → {replaced.Definition.Name}"
                        });
                    } else {
                        // Fallback: formula exists but has no dependencies and is not constant (edge case)
                        logs.Add(new LogEntry { Item = $"Replaced {currName} → {replaced.Definition.Name}" });
                        mapping.IsProcessed = true;
                    }
                } catch (Exception ex) {
                    logs.Add(new LogEntry { Item = $"{currName} → {mapping.NewName}", Error = ex.Message });
                }
            }
        }

        return new OperationLog(this.Name, logs);
    }
}