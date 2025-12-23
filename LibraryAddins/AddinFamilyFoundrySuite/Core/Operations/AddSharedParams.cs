using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddSharedParams(
    IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams
) : DocOperation {
    private IEnumerable<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)>
        SharedParams {
        get;
    } = sharedParams;

    public override string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        foreach (var sharedParam in this.SharedParams) {
            var name = sharedParam.externalDefinition.Name;

            try {
                var addedParam = doc.AddSharedParameter(sharedParam);
                logs.Add(new LogEntry(addedParam.Definition.Name).Success("Added"));
            } catch (Exception ex) {
                logs.Add(new LogEntry(name).Error(ex));
            }
        }

        return new OperationLog(this.Name, logs);
    }
}