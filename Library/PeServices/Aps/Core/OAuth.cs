using PeServices.Aps.Models;

namespace PeServices.Aps.Core;

/// <summary>
///     Autodesk Platform Services Authentication Handler. Instances of this class with the same credentials
///     will share their tokens between each other. In other words, refreshing a token from one instance will
///     refresh the token for all other instances with the same credentials.
/// </summary>
/// <remarks>
///     Uses a TCP listener rather than HTTP in order to sidestep need for admin privileges.
/// </remarks>
public class OAuth(TokenProviders.IAuth tokenProvider) {
    /// <summary>
    ///     A static cache of tokens and their expiration times, keyed by client ID. Allows for multiple
    ///     OAuth instances to use the same token if the OAuth instances' credentials are the same.
    /// </summary>
    private static readonly Dictionary<string, (string Token, DateTime ExpiresAt)> TokenCache = new();

    /// <summary>Lock for thread-safe access to the cache</summary>
    private static readonly object CacheLock = new();

    private readonly TokenProviders.IAuth _tokenProvider = tokenProvider;

    /// <summary>
    ///     Gets a valid access token, refreshing if necessary
    /// </summary>
    /// <exception>Thrown if authentication was denied or failed</exception>
    public string GetToken() {
        var clientId = this._tokenProvider.GetClientId();
        if (string.IsNullOrEmpty(clientId))
            throw new Exception("ClientId is not set");

        lock (CacheLock) {
            if (TokenCache.TryGetValue(clientId, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
                return cached.Token;
        }

        var clientSecret = this._tokenProvider.GetClientSecret();
        var tcs = new TaskCompletionSource<Result<string>>();

        OAuthHandler.Invoke3LeggedOAuth(clientId, clientSecret, bearer => {
            try {
                if (bearer == null) {
                    tcs.SetResult(new Exception(
                        "Authentication was denied or failed. Please try again." +
                        "In the event of unexpected failure after 2 or 3 attempts then contact the developer."));
                } else {
                    var expiresAt = DateTime.UtcNow.AddSeconds(bearer.ExpiresIn ?? 0);
                    lock (CacheLock) TokenCache[clientId] = (bearer.AccessToken, expiresAt);
                    tcs.SetResult(bearer.AccessToken);
                }
            } catch (Exception ex) {
                tcs.SetResult(new Exception(ex.Message));
            }
        });

        tcs.Task.Wait();
        var (token, tokenErr) = tcs.Task.Result;
        return tokenErr is not null ? throw tokenErr : token;
    }
}