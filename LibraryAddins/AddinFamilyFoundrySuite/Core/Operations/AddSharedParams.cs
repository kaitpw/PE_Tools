using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddSharedParams(
    List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams,
    List<string> sharedParamsToSkip = null
) : DocOperation {
    private List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> SharedParams {
        get;
    } = sharedParams;

    private List<string> SharedParamsToSkip { get; } = sharedParamsToSkip;
    public override string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        foreach (var sharedParam in this.SharedParams) {
            var name = sharedParam.externalDefinition.Name;
            if (this.SharedParamsToSkip != null
                && this.SharedParamsToSkip.Contains(name)) continue;

            try {
                var addedParam = doc.AddSharedParameter(sharedParam);
                logs.Add(new LogEntry { Item = addedParam.Definition.Name });
            } catch (Exception ex) {
                logs.Add(new LogEntry { Item = name, Error = ex.Message });
            }
        }

        return new OperationLog(this.Name, logs);
    }
}