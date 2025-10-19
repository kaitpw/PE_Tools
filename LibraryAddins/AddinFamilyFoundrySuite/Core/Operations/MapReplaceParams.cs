using PeExtensions.FamManager;
using PeRevit.Utils;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapReplaceParams : IOperation<MapParamsSettings> {
    private readonly List<ParamModelRes> _apsParams;

    public MapReplaceParams(List<ParamModelRes> apsParams) => this._apsParams = apsParams;

    public MapParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;

    public string Description => "Replace a family's existing parameters with APS shared parameters";

    public OperationLog Execute(Document doc) {
        var log = new OperationLog(this.GetType().Name);
        var fm = doc.FamilyManager;

        var defFile = Utils.MakeTempSharedParamTxt(doc);
        var group = defFile.Groups.get_Item("Parameters") ?? defFile.Groups.Create("Parameters");

        foreach (var mapping in this.Settings.MappingData) {
            var apsParam = this._apsParams.FirstOrDefault(p => p.Name == mapping.NewName);
            if (apsParam == null) {
                log.Entries.Add(new LogEntry { Item = mapping.NewName, Error = "APS parameter not found in cache" });
                continue;
            }

            try {
                var currentParam = fm.FindParameter(mapping.CurrName);
                if (currentParam == null) continue; // skip silently, errors will show in MapParams operation
                if (ParameterUtils.IsBuiltInParameter(currentParam.Id))
                    continue; // skip silently, MapParams will handle

                var newParam = apsParam.GetExternalDefinition(group);
                if (currentParam.Definition.GetDataType() != newParam.GetDataType())
                    continue; // skip silently, MapParams will handle

                var replaced = fm.ReplaceParameter(
                    currentParam,
                    newParam,
                    apsParam.DownloadOptions.GroupTypeId,
                    apsParam.DownloadOptions.IsInstance
                );
                this.Settings.MappingData.First(m => m.NewName == mapping.NewName).isProcessed = true;
                log.Entries.Add(new LogEntry { Item = $"{mapping.CurrName} → {replaced.Definition.Name}" });
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry { Item = mapping.NewName, Error = ex.Message });
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