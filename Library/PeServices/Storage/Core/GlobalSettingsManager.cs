using Json.Schema.Generation;
using PeServices.Storage.Models;
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
        new(new Json<GlobalSettings>(this._settingsFilePath));


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
    }
}