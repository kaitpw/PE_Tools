using Json.Schema.Generation;
using PeUtils.Files;

namespace PeServices.Storage.Core;

public class GlobalSettingsManager {
    private readonly string _basePath;
    private readonly string _settingsFilePath;


    public GlobalSettingsManager(string basePath) {
        this._basePath = basePath;
        this._settingsFilePath = Path.Combine(this._basePath, "settings.json");
        _ = Directory.CreateDirectory(this._basePath);
    }

    public JsonReader<GlobalSettings> Json() =>
        new(new Json<GlobalSettings>(this._settingsFilePath, true));


    /// <summary> Base interface for all settings classes. Provides global settings properties.</summary>
    public class GlobalSettings {
        [Description(
            "The desktop-app client id of the Autodesk Platform Services app. If none exists yet, make a 'Desktop App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        [Required]
        public string ApsDesktopClientId1 { get; set; } = "";

        [Description(
            "The web-app client id of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        [Required]
        public string ApsWebClientId1 { get; set; } = "";

        [Description(
            "The web-app client secret of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        [Required]
        public string ApsWebClientSecret1 { get; set; } = "";

        [Description(
            "The account ID derived from an 'id' field returned by `project/v1/hubs` but with the 'b.' prefix sliced off. If left empty, the first item of 'data' will be used.")]
        [Required]
        public string Bim360AccountId { get; set; } = "";

        [Description(
            "The group ID derived from an 'id' field returned by `parameters/v1/accounts/<accountId>/groups`. If left empty, the first item of 'results' will be used.")]
        [Required]
        public string ParamServiceGroupId { get; set; } = "";

        [Description(
            "The collection ID derived from an 'id' field returned by `parameters/v1/accounts/<accountId>/groups/<groupId>/collections`. If left empty, the first item of 'results' will be used.")]
        public string ParamServiceCollectionId { get; set; } = "";
    }
}