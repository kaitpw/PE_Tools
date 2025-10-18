using AddinFamilyFoundrySuite.Core.Operations.Settings;
using System.ComponentModel.DataAnnotations;
using PeExtensions.FamManager;


namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteParams : IOperation<DeleteParamsSettings> {
    public DeleteParams(List<string> ExcludeNamesEqualing) =>
        this.ExternalExcludeNamesEqualing = ExcludeNamesEqualing;

    public List<string> ExternalExcludeNamesEqualing { get; set; } = [];
    public DeleteParamsSettings Settings { get; set; }
    public OperationType Type => OperationType.Doc;
    public string Name => "Delete Parameters";
    public string Description => "Recursively delete parameters from the family by name";

    public OperationLog Execute(Document doc) {
        var log = new OperationLog();
        foreach (var name in this.ExternalExcludeNamesEqualing) {
            try {
                var param = doc.FamilyManager.FindParameter(name);
                if (param is null) {
                    log.Entries.Add(new LogEntry { Item = name, Error = "Parameter not found" });
                    continue;
                }
                if (ParameterUtils.IsBuiltInParameter(param.Id)) continue;


                doc.FamilyManager.RemoveParameter(param);
                log.Entries.Add(new LogEntry { Item = name });
            } catch (Exception ex) {
                log.Entries.Add(new LogEntry { Item = name, Error = ex.Message });
            }
        }
        return log;
    }
}

public class DeleteParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
}