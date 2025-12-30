using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.OperationGroups;

public class AddAndMapSharedParams : OperationGroup<MapParamsSettings> {
    public AddAndMapSharedParams(
        MapParamsSettings settings,
        IEnumerable<SharedParameterDefinition> sharedParams
    ) : base(
        "Map and add shared parameters (replace, add unmapped, and remap)",
        InitializeOperations(settings, sharedParams)
    ) {
    }

    private static List<IOperation> InitializeOperations(
        MapParamsSettings settings,
        IEnumerable<SharedParameterDefinition> sharedParams
    ) => [
        new MapReplaceParams(settings, sharedParams),
        new AddUnmappedSharedParams(settings, sharedParams),
        new MapParams(settings),
        new BacklinkParamsToBuiltIn(settings)
    ];
}

public class AddUnmappedSharedParams : DocOperation<MapParamsSettings> {
    private readonly IEnumerable<SharedParameterDefinition> _sharedParams;

    public AddUnmappedSharedParams(
        MapParamsSettings settings,
        IEnumerable<SharedParameterDefinition> sharedParams
    ) : base(settings) => this._sharedParams = sharedParams;

    public override string Description =>
        "Add shared parameters that are not already processed by a previous operation";

    public override OperationLog Execute(FamilyDocument doc,
        FamilyProcessingContext processingContext,
        OperationContext groupContext) {
        // Get already-processed params from GroupContext (completed by MapReplaceParams)
        var processedParams = groupContext.All
            .Where(e => e.IsComplete)
            .Select(e => e.Name)
            .ToHashSet() ?? [];
        var existingParams = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Select(p => p.Definition.Name)
            .ToHashSet();
        var addParams = this._sharedParams
            .Where(p => !processedParams.Contains(p.ExternalDefinition.Name))
            .Where(p => !existingParams.Contains(p.ExternalDefinition.Name));

        var addSharedParams = new AddSharedParams(addParams) { Name = this.Name };
        return addSharedParams.Execute(doc, processingContext, groupContext);
    }
}