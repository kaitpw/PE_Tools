using PeExtensions.FamDocument;
using PeRevit.Utils;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddApsParams : IOperation<AddApsParamsSettings> {
    public AddApsParams(List<ParamModelRes> apsParams, DefinitionGroup group, List<string> apsParamsToSkip = null) {
        this._apsParams = apsParams;
        this._group = group;
        this._apsParamsToSkip = apsParamsToSkip;
    }

    private List<ParamModelRes> _apsParams { get; }
    private DefinitionGroup _group { get; }
    private List<string> _apsParamsToSkip { get; }

    public AddApsParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;

    public string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public OperationLog Execute(Document doc) {
        var logs = new List<LogEntry>();

        foreach (var apsParam in this._apsParams) {
            if (this._apsParamsToSkip != null
                && this._apsParamsToSkip.Contains(apsParam.Name)) continue;

            try {
                var addedParam = doc.AddApsParameter(apsParam, this._group);
                logs.Add(new LogEntry { Item = addedParam.Definition.Name });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = apsParam.Name, Error = ex.Message });
            }
        }

        return new OperationLog(((IOperation)this).Name, logs);
    }
}

public class AddApsParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
}