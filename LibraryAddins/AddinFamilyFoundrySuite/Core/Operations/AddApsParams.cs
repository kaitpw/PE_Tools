using PeExtensions.FamDocument;
using PeRevit.Utils;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddApsParams : IOperation<AddApsParamsSettings> {
    public AddApsParams(List<ParamModelRes> apsParams, List<string> apsParamsToSkip = null) {
        this._apsParams = apsParams;
        this._apsParamsToSkip = apsParamsToSkip;
    }

    private List<ParamModelRes> _apsParams { get; }
    private List<string> _apsParamsToSkip { get; }

    public AddApsParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;

    public string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public OperationLog Execute(Document doc) {
        var log = new OperationLog(this.GetType().Name);

        var fm = doc.FamilyManager;

        var defFile = Utils.MakeTempSharedParamTxt(doc);
        var group = defFile.Groups.get_Item("Parameters") ?? defFile.Groups.Create("Parameters");

        foreach (var apsParam in this._apsParams) {
            if (this._apsParamsToSkip != null
                && this._apsParamsToSkip.Contains(apsParam.Name)) continue;

            try {
                var (sharedParam, sharedParamErr) = doc.AddApsParameter(fm, group, apsParam);
                if (sharedParamErr is not null) {
                    // Do Not port this code block to the new combined method
                    var (slowParam, slowErr) = doc.AddApsParameterSlow(apsParam);
                    if (slowErr != null) {
                        log.Entries.Add(new LogEntry { Item = apsParam.Name, Error = slowErr.Message });
                    } else
                        log.Entries.Add(new LogEntry { Item = slowParam.Name });
                } else
                    log.Entries.Add(new LogEntry { Item = sharedParam.Name });
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry { Item = apsParam.Name, Error = ex.Message });
            }
        }

        try {
            if (File.Exists(defFile.Filename)) File.Delete(defFile.Filename);
        } catch {
            Debug.WriteLine("Failed to delete temporary shared param file.");
        }

        return log;
    }
}

public class AddApsParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
}