using PeRevitUI;
using PeServices.Aps;
using PeServices.Storage;

namespace AddinCmdApsAuthNormal;

[Transaction(TransactionMode.Manual)]
public class CmdApsAuthNormal : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        try {
            var storage = new Storage("ApsAuthNormal");
            var settings = storage.Settings().Json<ApsAuthNormal>().Read();
            var auth = new Aps(settings);
            var token = auth.GetToken();
            new Balloon().AddDebug(new StackFrame(), Log.INFO, token).Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(Log.ERR, ex.Message).Show();
            return Result.Failed;
        }
    }
}

public class ApsAuthNormal : Storage.BaseSettings, Aps.ITokenProvider {
    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsWebClientId1;
    public string GetClientSecret() => Storage.GlobalSettings().Json().Read().ApsWebClientSecret1;
}