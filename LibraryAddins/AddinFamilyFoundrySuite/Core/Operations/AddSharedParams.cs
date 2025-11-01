using PeExtensions.FamDocument;

namespace AddinFamilyFoundrySuite.Core.Operations;

public class AddSharedParams : DocOperation<DefaultOperationSettings> {
    public AddSharedParams(
        DefaultOperationSettings settings,
        List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> sharedParams,
        List<string> sharedParamsToSkip = null
    ) : base(settings) {
        this._sharedParams = sharedParams;
        this._sharedParamsToSkip = sharedParamsToSkip;
    }

    private List<(ExternalDefinition externalDefinition, ForgeTypeId groupTypeId, bool isInstance)> _sharedParams {
        get;
    }

    private List<string> _sharedParamsToSkip { get; }
    public override string Description => "Download and add shared parameters from Autodesk Parameters Service";

    public override OperationLog Execute(FamilyDocument doc) {
        var logs = new List<LogEntry>();

        foreach (var sharedParam in this._sharedParams) {
            var name = sharedParam.externalDefinition.Name;
            if (this._sharedParamsToSkip != null
                && this._sharedParamsToSkip.Contains(name)) continue;

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