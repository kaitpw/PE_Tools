using PeExtensions.FamManager;
using PeRevit.Utils;
using ParamModelRes = PeServices.Aps.Models.ParametersApi.Parameters.ParametersResult;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class MapReplaceParams : IOperation<MapParamsSettings> {
    private readonly Dictionary<string, ParamModelRes> _apsParamsByName;
    private readonly DefinitionGroup _group;

    public MapReplaceParams(List<ParamModelRes> apsParams, DefinitionGroup group) {
        this._apsParamsByName = apsParams.ToDictionary(p => p.Name);
        this._group = group;
    }

    public MapParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;

    public string Description => "Replace a family's existing parameters with APS shared parameters";

    public OperationLog Execute(Document doc) {
        var log = new OperationLog(this.GetType().Name);
        var fm = doc.FamilyManager;

        foreach (var mapping in this.Settings.MappingData) {
            if (!this._apsParamsByName.TryGetValue(mapping.NewName, out var apsParam)) {
                log.Entries.Add(new LogEntry { Item = mapping.NewName, Error = "APS parameter not found in cache" });
                continue;
            }

            try {
                var currentParam = fm.FindParameter(mapping.CurrName);
                if (currentParam == null) continue; // skip silently, errors will show in MapParams operation
                if (ParameterUtils.IsBuiltInParameter(currentParam.Id)) continue;

                var dlOpts = apsParam.DownloadOptions;
                if (currentParam.Definition.GetDataType() != dlOpts.GetSpecTypeId()) continue;
                var newParam = dlOpts.GetExternalDefinition(this._group);

                var replaced = fm.ReplaceParameter(
                    currentParam,
                    newParam,
                    dlOpts.GetGroupTypeId(),
                    dlOpts.IsInstance
                );
                this.Settings.MappingData.First(m => m.NewName == mapping.NewName).isProcessed = true;
                log.Entries.Add(new LogEntry { Item = $"{mapping.CurrName} → {replaced.Definition.Name}" });
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry { Item = mapping.NewName, Error = ex.Message });
            }
        }

        return log;
    }
}