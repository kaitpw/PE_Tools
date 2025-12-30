using AddinFamilyFoundrySuite.Core;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;
using PeExtensions.FamManager;
using PeExtensions.FamParameter.Formula;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapReplaceParams : DocOperation<MapParamsSettings> {
    private readonly Dictionary<string, SharedParameterDefinition> _sharedParamsDict;

    private readonly List<ForgeTypeId> IgnoreCoercionDataTypes = new() {
        SpecTypeId.Number, SpecTypeId.String.Text, SpecTypeId.Length
    };

    public MapReplaceParams(
        MapParamsSettings settings,
        IEnumerable<SharedParameterDefinition> sharedParams
    ) : base(settings) => this._sharedParamsDict = sharedParams.ToDictionary(p => p.ExternalDefinition.Name);

    public override string Description => "Replace a family's existing parameters with APS shared parameters";

    public override OperationLog Execute(FamilyDocument doc, FamilyProcessingContext processingContext, OperationContext groupContext) {
        var fm = doc.FamilyManager;

        foreach (var mapping in this.Settings.MappingData) {
            var log = groupContext.GetOrCreate(mapping.NewName);
            if (log.IsComplete) continue;

            if (!this._sharedParamsDict.TryGetValue(mapping.NewName, out var sharedParam)) {
                _ = log.Skip("Shared parameter not found");
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

                    if (currentParam.Definition.GetDataType() != sharedParam.ExternalDefinition.GetDataType()) continue;

                    var replaced = fm.ReplaceParameter(
                        currentParam,
                        sharedParam.ExternalDefinition,
                        sharedParam.GroupTypeId,
                        sharedParam.IsInstance
                    );

                    foundMatch = true;

                    // Formula contains parameters: No further processing if formula exists
                    // Formula is constant: Unset formula but DO NOT mark as processed, instead set CurrName to NewName 
                    // (CurrName is gone at this point). This allows SetValue to coerce later which is
                    // necessary for electrical parameters using ElectricalCoercionStrategy)
                    var parameters = doc.FamilyManager.Parameters;
                    var singleReference = parameters.TryGetSingleReference(replaced.Formula);
                    if (singleReference != null) {
                        var refName = singleReference.Definition.Name;
                        var refIsBuiltIn = ParameterUtils.IsBuiltInParameter(singleReference.Id);
                        var refIsInCurrNames = mapping.CurrNames.Contains(refName);

                        if (refIsBuiltIn && refIsInCurrNames) {
                            // NewName inherited formula pointing to a built-in that's in CurrNames.
                            // Unset formula on NewName, let MapParams copy value from built-in and create backlink.
                            _ = doc.UnsetFormula(replaced);
                            // Store the built-in ref name for MapParams to use>
                            _ = log.Defer($"Replaced {currName}, deferred backlink to {refName}");
                        } else {
                            // Standard unwrap: formula points to non-built-in or not in CurrNames
                            _ = doc.UnsetFormula(singleReference);
                            _ = log.Success($"{currName} → {replaced.Definition.Name}");
                        }
                    } else if (replaced.Formula == null ||
                               parameters.IsConstant(replaced.Formula)) {
                        _ = doc.UnsetFormula(replaced);
                        // skip datatypes that will never need coercion, boosts speed and cleans logs
                        _ = this.IgnoreCoercionDataTypes.Contains(replaced.Definition.GetDataType())
                            ? log.Success($"Replaced {currName} → {replaced.Definition.Name}")
                            : log.Defer($"Replaced {currName}, awaiting coercion");
                    } else {
                        // Fallback: formula exists but has no dependencies and is not constant (edge case)
                        _ = log.Success($"Replaced {currName} → {replaced.Definition.Name}");
                    }
                } catch (Exception) {
                    _ = log.Defer($"{currName} → {mapping.NewName}"); // allow retrying 
                }
            }
        }

        return new OperationLog(this.Name, groupContext.TakeSnapshot());
    }
}