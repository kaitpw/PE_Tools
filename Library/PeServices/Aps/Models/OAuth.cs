using Json.Schema.Generation;

namespace PeServices.Aps.Models;

public class OAuth {
    /// <summary>Interface for providing APS authentication credentials to the OAuth class</summary>
    public interface IApsTokenProvider {
        string GetClientId();
        string GetClientSecret();
    }

    public abstract class BaseSettingsPKCE : SettingsManager.BaseSettings, IApsTokenProvider {
        [Description(
            "The client id of the Autodesk Platform Services app. If none exists yet, make a 'Desktop App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        [Required]
        public string ApsClientId { get; set; } = "";

        string IApsTokenProvider.GetClientId() => this.ApsClientId;
        string IApsTokenProvider.GetClientSecret() => null; // PKCE flow doesn't use client secret
    }

    public abstract class BaseSettingsNormal : SettingsManager.BaseSettings, IApsTokenProvider {
        [Description(
            "The client id of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        [Required]
        public string ApsClientId { get; set; } = "";

        [Description(
            "The client secret of the Autodesk Platform Services app. If none exists yet, make a 'Traditional Web App' at https://aps.autodesk.com/hubs/@personal/applications/")]
        public string ApsClientSecret { get; set; } = "";

        string IApsTokenProvider.GetClientId() => this.ApsClientId;
        string IApsTokenProvider.GetClientSecret() => this.ApsClientSecret;
    }
}