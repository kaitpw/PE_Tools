using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.OperationGroups;

public class MapAndAddSharedParams : OperationGroup<MapParamsSettings> {
    public MapAndAddSharedParams(
        MapParamsSettings settings,
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : base(
        "Map and add shared parameters (replace, add unmapped, and remap)",
        [
            new MapReplaceParams(settings, sharedParams),
            new AddUnmappedSharedParams(settings, sharedParams),
            new MapParams(settings)
        ]
    ) {
    }
}

public class AddUnmappedSharedParams : DocOperation<MapParamsSettings> {
    private readonly List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>
        _sharedParams;

    public AddUnmappedSharedParams(
        MapParamsSettings settings,
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : base(settings) => this._sharedParams = sharedParams;

    public override string Description =>
        "Add shared parameters that are not already processed by a previous operation";

    public override OperationLog Execute(FamilyDocument doc) {
        // Compute skip list from already-processed mappings
        var sharedParamsToSkip = this.Settings.MappingData
            .Where(m => m.isProcessed)
            .Select(m => m.NewName)
            .ToList();

        var addsharedParams =
            new AddSharedParams(this._sharedParams, sharedParamsToSkip) { Name = this.Name };
        return addsharedParams.Execute(doc);
    }
}