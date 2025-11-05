using PeExtensions.FamDocument;
using PeExtensions.FamManager;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class DeleteParams(DeleteParamsSettings settings, List<string> excludeNamesEqualing)
    : DocOperation<DeleteParamsSettings>(settings) {
    public List<string> ExternalExcludeNamesEqualing { get; set; } = excludeNamesEqualing;
    public override string Description => "Recursively delete parameters from the family by name";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();
        foreach (var name in this.ExternalExcludeNamesEqualing) {
            try {
                var param = doc.FamilyManager.FindParameter(name);
                if (param is null) {
                    logs.Add(new LogEntry { Item = name, Error = "Parameter not found" });
                    continue;
                }

                if (ParameterUtils.IsBuiltInParameter(param.Id)) continue;


                doc.FamilyManager.RemoveParameter(param);
                logs.Add(new LogEntry { Item = name });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = name, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}

public class DeleteParamsSettings : IOperationSettings {
    public bool Enabled { get; init; } = true;
}