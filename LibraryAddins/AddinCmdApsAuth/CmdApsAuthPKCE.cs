using PE_Tools.Properties;
using PeRevitUI;
using PeServices;
using PeServices.Aps;
namespace AddinCmdApsAuth;

[Transaction(TransactionMode.Manual)]
public class CmdApsAuthPKCE : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        try {
            var storage = new Storage("ApsAuthPKCE");
            var settings = storage.Settings().Json<ApsAuthSettingsPKCE>().Read();
            var auth = new OAuth(settings);
            var (token, tokenErr) = auth.GetToken();
            if (tokenErr is not null) throw tokenErr;
            new Balloon().Add(Balloon.Log.INFO, token).Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            new Balloon().Add(Balloon.Log.ERR, ex.Message).Show();
            return Result.Failed;
        }
    }

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "APS Auth (PKCE)",
            MethodBase.GetCurrentMethod().DeclaringType?.FullName,
            Resources.Blue_32,
            Resources.Blue_16,
            "Click this button to get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything."
        ).Data;
}