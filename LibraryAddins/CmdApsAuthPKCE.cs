using PE_Tools.Properties;
using PeRevitUI;
using PeServices.Aps;
using PeServices.Aps.Models;
using PeServices.Storage;

namespace AddinCmdApsAuthPKCE;

[Transaction(TransactionMode.Manual)]
public class CmdApsAuthPKCE : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        try {
            var storage = new Storage("ApsAuthPKCE");
            var settings = storage.Settings().Json<ApsAuthPkce>().Read();
            var aps = new Aps(settings);
            var token = aps.GetToken();
            new Balloon().AddDebug(new StackFrame(), Balloon.Log.INFO, token).Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(Balloon.Log.ERR, ex.Message).Show();
            return Result.Failed;
        }
    }
}

public class ApsAuthPkce : Storage.BaseSettings, OAuth.IApsTokenProvider {
    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;
}