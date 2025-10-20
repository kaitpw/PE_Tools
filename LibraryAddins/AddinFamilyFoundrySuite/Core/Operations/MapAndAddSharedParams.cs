
namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapAndAddSharedParams : ICompoundOperation<MapParamsSettings> {
    public MapAndAddSharedParams(
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams) =>
        this.Operations = [
            new MapReplaceParams(sharedParams),
            new AddUnmappedSharedParams(sharedParams),
            new MapParams()
        ];

    public List<IOperation<MapParamsSettings>> Operations { get; set; }
}

public class AddUnmappedSharedParams : IOperation<MapParamsSettings> {
    private readonly List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> _sharedParams;

    public AddUnmappedSharedParams(
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) => this._sharedParams = sharedParams;

    public MapParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;

    public string Description => "Add shared parameters that are not already processed by a previous operation";

    public OperationLog Execute(Document doc) {
        // Compute skip list from already-processed mappings
        var sharedParamsToSkip = this.Settings.MappingData
            .Where(m => m.isProcessed)
            .Select(m => m.NewName)
            .ToList();

        var addsharedParams = new AddSharedParams(this._sharedParams, sharedParamsToSkip) { Settings = new AddSharedParamsSettings() };
        return addsharedParams.Execute(doc);
    }
}