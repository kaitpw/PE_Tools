using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapAndAddApsParams : ICompoundOperation<MapParamsSettings> {
    public MapAndAddApsParams(List<ParamModelRes> apsParams) =>
        this.Operations = [
            new MapReplaceParams(apsParams),
            new AddUnmappedApsParams(apsParams),
            new MapParams()
        ];

    public List<IOperation<MapParamsSettings>> Operations { get; set; }
}

public class AddUnmappedApsParams : IOperation<MapParamsSettings> {
    private readonly List<ParamModelRes> _apsParams;

    public AddUnmappedApsParams(List<ParamModelRes> apsParams) =>
        this._apsParams = apsParams;

    public MapParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;

    public string Description => "Add APS parameters that are not already processed by a previous operation";

    public OperationLog Execute(Document doc) {
        // Compute skip list from already-processed mappings
        var apsParamsToSkip = this.Settings.MappingData
            .Where(m => m.isProcessed)
            .Select(m => m.NewName)
            .ToList();

        var addApsParams = new AddApsParams(this._apsParams, apsParamsToSkip) { Settings = new AddApsParamsSettings() };
        return addApsParams.Execute(doc);
    }
}