namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapAndAddSharedParams : OperationGroup<MapParamsSettings> {
    public MapAndAddSharedParams(
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : base(
        description: "Map and add shared parameters (replace, add unmapped, and remap)",
        operations: [
            new MapReplaceParams(sharedParams),
            new AddUnmappedSharedParams(sharedParams),
            new MapParams()
        ]
    ) {
    }
}

public class AddUnmappedSharedParams : DocOperation<MapParamsSettings> {
    private readonly List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>
        _sharedParams;

    public AddUnmappedSharedParams(
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) => this._sharedParams = sharedParams;

    public override string Description => "Add shared parameters that are not already processed by a previous operation";

    public override OperationLog Execute(Document doc) {
        // Compute skip list from already-processed mappings
        var sharedParamsToSkip = this.Settings.MappingData
            .Where(m => m.isProcessed)
            .Select(m => m.NewName)
            .ToList();

        var addsharedParams =
            new AddSharedParams(this._sharedParams, sharedParamsToSkip) {
                Name = this.Name,
                Settings = new AddSharedParamsSettings(),
            };
        return addsharedParams.Execute(doc);
    }
}