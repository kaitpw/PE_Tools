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
            new Ballogger().AddDebug(Log.INFO, new StackFrame(), token).Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, null, ex.Message).Show();
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
            new Ballogger().AddDebug(Log.INFO, new StackFrame(), token).Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Ballogger().Add(Log.ERR, null, ex.Message).Show();
            return Result.Failed;
        }
    }
}

public class ApsAuthNormal : Aps.IOAuthTokenProvider {
    public string GetClientId() => Storage.Global().SettingsFile().Read().ApsWebClientId1;
    public string GetClientSecret() => Storage.Global().SettingsFile().Read().ApsWebClientSecret1;
}

public class ApsAuthPkce : Aps.IOAuthTokenProvider {
    public string GetClientId() => Storage.Global().SettingsFile().Read().ApsDesktopClientId1;
    public string GetClientSecret() => null;
}