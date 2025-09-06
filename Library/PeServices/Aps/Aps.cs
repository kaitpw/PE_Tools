using PeServices.Aps.Core;
using System.Net.Http;
using System.Net.Http.Headers;
using OAuth = PeServices.Aps.Models.OAuth;

namespace PeServices.Aps;

public class Aps(OAuth.IApsTokenProvider tokenProvider) {
    private readonly Core.OAuth _oAuth = new(tokenProvider);

    private HttpClient HttpClient => new() {
        BaseAddress = new Uri("https://developer.api.autodesk.com/"),
        DefaultRequestHeaders = {
            Accept = { new MediaTypeWithQualityHeaderValue("application/json") },
            Authorization = new AuthenticationHeaderValue("Bearer", this._oAuth.GetToken())
        }
    };

    public Parameters Parameters() => new(this.HttpClient);
    public Hubs Hubs() => new(this.HttpClient);
    public string GetToken() => this._oAuth.GetToken();

    public class BaseSettingsPkce : OAuth.BaseSettingsPKCE {
    }

    public class BaseSettingsNormal : OAuth.BaseSettingsNormal {
    }

    // public Models.OAuth ApsBaseSettings(): SettingsManager.BaseSettings => new();
}