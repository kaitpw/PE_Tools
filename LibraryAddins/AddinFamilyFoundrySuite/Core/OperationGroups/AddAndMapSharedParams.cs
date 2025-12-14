using AddinFamilyFoundrySuite.Core.Operations;
using AddinFamilyFoundrySuite.Core.OperationSettings;
using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.OperationGroups;

public class AddAndMapSharedParams : OperationGroup<MapParamsSettings> {
    public AddAndMapSharedParams(
        MapParamsSettings settings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
    ) : base(
        "Map and add shared parameters (replace, add unmapped, and remap)",
        InitializeOperations(settings, sharedParams, out var sharedState)
    ) => this.SharedState = sharedState;

    /// <summary>
    ///     Shared state for coordinating mapping operations across replace, add, and remap steps.
    /// </summary>
    public MapParamsSharedState SharedState { get; }

    private static List<IOperation<MapParamsSettings>> InitializeOperations(
        MapParamsSettings settings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams,
        out MapParamsSharedState sharedState
    ) {
        sharedState = new MapParamsSharedState(settings.MappingData);

        return [
            new MapReplaceParams(settings, sharedState, sharedParams),
            new AddUnmappedSharedParams(settings, sharedParams, sharedState),
            new MapParams(settings, sharedState),
            new BacklinkParamsToBuiltIn(settings, sharedState)
        ];
    }
}

public class AddUnmappedSharedParams : DocOperation<MapParamsSettings> {
    private readonly IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>
        _sharedParams;

    private readonly MapParamsSharedState _sharedState;

    public AddUnmappedSharedParams(
        MapParamsSettings settings,
        IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams,
        MapParamsSharedState sharedState = null
    ) : base(settings) {
        this._sharedParams = sharedParams;
        this._sharedState = sharedState;
    }

    public override string Description =>
        "Add shared parameters that are not already processed by a previous operation";

    public override OperationLog Execute(FamilyDocument doc) {
        var processedParams = this._sharedState?.GetCurrentMappings()
            .Where(m => m.IsProcessed)
            .Select(m => m.NewName)
            .ToHashSet() ?? [];
        var existingParams = doc.FamilyManager.Parameters
            .OfType<FamilyParameter>()
            .Select(p => p.Definition.Name)
            .ToHashSet();
        var addParams = this._sharedParams
            .Where(p => !processedParams.Contains(p.externalDefinition.Name))
            .Where(p => !existingParams.Contains(p.externalDefinition.Name));

        var addSharedParams = new AddSharedParams(addParams) { Name = this.Name };
        return addSharedParams.Execute(doc);
    }
}

/// <summary>
///     Shared state for coordinating mapping operations across the operation chain.
///     Creates fresh mutable mappings for each family execution.
/// </summary>
public class MapParamsSharedState {
    private readonly IEnumerable<MappingData> _sourceMappings;
    private List<MappingData> _currentMappings;

    public MapParamsSharedState(IEnumerable<MappingData> mappingData) => this._sourceMappings = mappingData;

    /// <summary>
    ///     Creates fresh mutable mappings for a new family execution.
    ///     Called by the first operation (MapReplaceParams).
    /// </summary>
    public List<MappingData> CreateFreshMappings() {
        this._currentMappings = this._sourceMappings.Select(m => new MappingData {
            CurrNames = m.CurrNames.ToList(),
            NewName = m.NewName,
            MappingStrategy = m.MappingStrategy,
            IsProcessed = false
        }).ToList();
        return this._currentMappings;
    }

    /// <summary>
    ///     Gets current mappings (for subsequent operations in the chain).
    /// </summary>
    public List<MappingData> GetCurrentMappings() => this._currentMappings ?? this.CreateFreshMappings();
}