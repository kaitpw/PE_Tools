#nullable enable

namespace PeServices;

/// <summary>
///     Interface for providing APS authentication credentials
/// </summary>
public interface IApsTokenProvider {
    /// <summary>
    ///     Gets the client ID for APS authentication
    /// </summary>
    string GetClientId();

    /// <summary>
    ///     Gets the client secret for APS authentication if available
    /// </summary>
    string? GetClientSecret();
}

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
                    lock (CacheLock) {
                        TokenCache[clientId] = (bearer.AccessToken, expiresAt);
                    }
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