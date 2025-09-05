#nullable enable

using Json.Schema.Generation;

namespace PeServices;

/// <summary>
///     Autodesk Platform Services Authentication Handler.
/// </summary>
public class ApsAuth {
    private static readonly Dictionary<string, (string Token, DateTime ExpiresAt)> TokenCache = new();
    private static readonly object CacheLock = new();
    private readonly object _lock = new();
    private readonly IApsTokenProvider _tokenProvider;

    /// <summary>
    ///     Creates a new instance of ApsAuth using the provided token provider
    /// </summary>
    /// <param name="tokenProvider">Provider for APS authentication credentials</param>
    public ApsAuth(IApsTokenProvider tokenProvider) => this._tokenProvider = tokenProvider;

    /// <summary>
    ///     Gets a valid access token, refreshing if necessary
    /// </summary>
    public Result<string> GetToken() {
        var clientId = this._tokenProvider.GetClientId();
        if (string.IsNullOrEmpty(clientId))
            return new Exception("ClientId is not set");

        lock (CacheLock) {
            if (TokenCache.TryGetValue(clientId, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
                return cached.Token;
        }

        var clientSecret = this._tokenProvider.GetClientSecret();
        var tcs = new TaskCompletionSource<Result<string>>();

        OAuthHandler.Invoke3LeggedOAuth(clientId, clientSecret, bearer => {
            try {
                if (bearer == null)
                    tcs.SetResult(new Exception("Authentication was denied or failed. Please try again."));
                else if (bearer.ExpiresIn == null)
                    tcs.SetResult(new Exception("Token expiration time not provided"));
                else {
                    var expiresAt = DateTime.UtcNow.AddSeconds(bearer.ExpiresIn.Value);
                    lock (CacheLock) TokenCache[clientId] = (bearer.AccessToken, expiresAt);
                    tcs.SetResult(bearer.AccessToken);
                }
            } catch (Exception ex) {
                tcs.SetResult(new Exception(ex.Message));
            }
        });

        try {
            return tcs.Task.Result;
        } catch (AggregateException ex) {
            throw ex.InnerException ?? ex;
        }
    }
}

/// <summary>
///     Interface for providing APS authentication credentials
/// </summary>
public interface IApsTokenProvider {
    string GetClientId();
    string? GetClientSecret();
}

/// <summary>
///     Settings for PKCE OAuth flow
/// </summary>
public class ApsAuthSettingsPKCE : SettingsManager.BaseSettings, IApsTokenProvider {
    [Description(
        "The client id of the Autodesk Platform Services app. If none exists yet, make a 'Desktop App' at https://aps.autodesk.com/hubs/@personal/applications/")]
    [Required]
    public string ApsClientId { get; set; } = "";

    string IApsTokenProvider.GetClientId() => this.ApsClientId;
    string? IApsTokenProvider.GetClientSecret() => null; // PKCE flow doesn't use client secret
}

/// <summary>
///     Settings for normal OAuth flow
/// </summary>
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