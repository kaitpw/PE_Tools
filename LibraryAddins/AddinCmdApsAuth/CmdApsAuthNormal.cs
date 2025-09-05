using Json.Schema.Generation;
using PE_Tools.Properties;
using PeRevitUI;
using PeServices;

namespace AddinCmdApsAuth;

[Transaction(TransactionMode.Manual)]
public class CmdApsAuthNormal : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        try {
            var storage = new Storage("ApsAuthNormal");
            var settings = storage.Settings().Json<ApsAuthSettingsNormal>().Read();
            // Make sure that we're testing normal flow
            if (string.IsNullOrEmpty(settings.ApsClientSecret))
                throw new Exception("Add Client secret. This addin is for testing normal flow, not PKCE flow.");
            var auth = new ApsAuth(settings);
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
            "APS Auth (Normal)",
            MethodBase.GetCurrentMethod().DeclaringType?.FullName,
            Resources.Blue_32,
            Resources.Blue_16,
            "Click this button to get an access token from Autodesk Platform Services. This is primarily for testing purposes, but running it will not hurt anything."
        ).Data;
}

#nullable enable

public class ApsAuthSettingsNormal : SettingsManager.BaseSettings, IApsTokenProvider {
    [Description(
        "The client id of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
    [Required]
    public string ApsClientId { get; set; } = "";

    [Description(
        "The client secret of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
    public string ApsClientSecret { get; set; } = "";

    string IApsTokenProvider.GetClientId() => this.ApsClientId;
    string? IApsTokenProvider.GetClientSecret() => this.ApsClientSecret;
}