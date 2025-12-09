using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.OperationGroups;

public class MapAndAddSharedParams : OperationGroup<RuntimeMapParamsSettings> {
    public MapAndAddSharedParams(
        MapParamsSettings settings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : this(new RuntimeMapParamsSettings(settings.MappingData), sharedParams) {
    }

    private MapAndAddSharedParams(
        RuntimeMapParamsSettings runtimeSettings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : base(
        "Map and add shared parameters (replace, add unmapped, and remap)",
        [
            new MapReplaceParams(runtimeSettings, sharedParams),
            new AddUnmappedSharedParams(runtimeSettings, sharedParams),
            new MapParams(runtimeSettings)
        ]
    ) {
    }
}

public class AddUnmappedSharedParams : DocOperation<RuntimeMapParamsSettings> {
    private readonly IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>
        _sharedParams;

    public AddUnmappedSharedParams(
        RuntimeMapParamsSettings runtimeSettings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : base(runtimeSettings) => this._sharedParams = sharedParams;

    public AddUnmappedSharedParams(
        MapParamsSettings settings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : this(new RuntimeMapParamsSettings(settings.MappingData), sharedParams) { }

    public override string Description =>
        "Add shared parameters that are not already processed by a previous operation";

    public override OperationLog Execute(FamilyDocument doc) {
        var processedParams = this.Settings.ProcessedMappingData.Select(m => m.NewName).ToHashSet();
        var addParams = this._sharedParams.Where(p => !processedParams.Contains(p.externalDefinition.Name));

        var addSharedParams = new AddSharedParams(addParams) { Name = this.Name };
        return addSharedParams.Execute(doc);
    }
}