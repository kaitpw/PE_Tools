using PeRevit.Ui;
using PeServices.Aps;
using PeServices.Storage;

namespace AddinApsAuthSuite;

[Transaction(TransactionMode.Manual)]
public class CmdApsAuthNormal : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        try {
            var storage = new Storage(nameof(CmdApsAuthNormal));
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


public class CmdApsAuthPKCE : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        try {
            var storage = new Storage(nameof(CmdApsAuthPKCE));
            var settings = storage.Settings().Json<ApsAuthPkce>().Read();
            var aps = new Aps(settings);
            var token = aps.GetToken();
            new Balloon().AddDebug(new StackFrame(), Log.INFO, token).Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(Log.ERR, ex.Message).Show();
            return Result.Failed;
        }
    }
}


public class ApsAuthNormal : Aps.IOAuthTokenProvider {
    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsWebClientId1;
    public string GetClientSecret() => Storage.GlobalSettings().Json().Read().ApsWebClientSecret1;
}

public class ApsAuthPkce : Aps.IOAuthTokenProvider {
    public string GetClientId() => Storage.GlobalSettings().Json().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;
}
