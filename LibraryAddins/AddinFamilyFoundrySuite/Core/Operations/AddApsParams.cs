using PeExtensions.FamDocument;
using PeServices.Storage;
using PeRevit.Utils;
using System.ComponentModel.DataAnnotations;
using ParamModel = PeServices.Aps.Models.ParametersApi.Parameters;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;
using AddinFamilyFoundrySuite.Core.Operations.Settings;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddApsParams : IOperation<AddApsParamsSettings> {
    public AddApsParams(List<ParamModelRes> apsParams) => this.ApsParams = apsParams;
    public List<ParamModelRes> ApsParams { get; set; }
    public AddApsParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Add APS Parameters";
    public string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public OperationLog Execute(Document doc) {
        var log = new OperationLog();

        var fm = doc.FamilyManager;

        var defFile = Utils.MakeTempSharedParamTxt(doc);
        var group = defFile.Groups.get_Item("Parameters") ?? defFile.Groups.Create("Parameters");

        foreach (var psParamInfo in this.ApsParams) {
            try {
                var (sharedParam, sharedParamErr) = doc.AddApsParameter(fm, group, psParamInfo);
                if (sharedParamErr is not null) {
                    // Do Not port this code block to the new combined method
                    var (slowParam, slowErr) = doc.AddApsParameterSlow(psParamInfo);
                    if (slowErr != null) {
                        log.Entries.Add(new LogEntry {
                            Item = psParamInfo.Name,
                            Error = slowErr.Message
                        });
                    } else {
                        log.Entries.Add(new LogEntry { Item = slowParam.Name });
                    }
                } else {
                    log.Entries.Add(new LogEntry { Item = sharedParam.Name });
                }
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry {
                    Item = psParamInfo.Name,
                    Error = ex.Message
                });
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
