using Json.Schema.Generation;
using PE_Tools.Properties;
using PeRevitUI;
using PeServices;

namespace PE_Tools;

[Transaction(TransactionMode.Manual)]
public class CmdApsAuth : IExternalCommand {
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements) {
        try {
            var storage = new Storage("ApsAuth");
            var settings = storage.Settings().Json<ApsAuthSettings>().Read();
            var (token, tokenError) = ApsAuth.Login(settings.ClientId, settings.ClientSecret);
            if (tokenError is not null) throw tokenError;
            new Balloon().Add(Balloon.Log.INFO, token).Show();
            return Result.Succeeded;
        } catch (Exception ex) {
            _ = TaskDialog.Show("ERROR with APS Authentication", ex.Message);
            return Result.Failed;
        }
    }

    internal static PushButtonData GetButtonData() =>
        new ButtonDataClass(
            "APS Auth",
            MethodBase.GetCurrentMethod().DeclaringType?.FullName,
            Resources.Blue_32,
            Resources.Blue_16,
            "Click this button to get an access token from Autodesk Platform Services"
        ).Data;
}

public class ApsAuthSettings : SettingsManager.BaseSettings {
    [Description(
        "The client id of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
    [Required]
    public string ClientId { get; set; } = "";

    [Description(
        "The client secret of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
    [Required]
    public string ClientSecret { get; set; } = "";
}