namespace PeServices;

/// <summary>
///     Autodesk Platform Services Authentication Handler.
/// </summary>
/// <remarks>
///     <c>GetToken()</c> returns the token for the last <c>ApsAuth.Login()</c> that was called.
/// </remarks>
/// <remarks>
///     TODO: In the far future, get this to be instance-based to handle multiple clientId and clientSecrets
///     TODO: Test if the check for changed client id/secret actually causes reinvocation of auth flow
/// </remarks>
public class ApsAuth {
    /// <summary>
    ///     NOTE: if this is not static, and Login() is called in an addin file (e.g. CmdApsAuth), the state will not be
    ///     persisted because calling login will make a new ApsAuth instance internally which resets _accessToken
    /// </summary>
    private static string? _accessToken;

    private static DateTime _expiresAt;

    public static Result<string> GetToken(string clientId, string clientSecret) {
        if (string.IsNullOrEmpty(clientId))
            return new Exception("ClientId is not set");

        if (_accessToken == null || DateTime.UtcNow >= _expiresAt)
            RefreshToken(clientId, clientSecret);

        return _accessToken;
    }

    private static void RefreshToken(string clientId, string clientSecret) {
        Exception asyncException = null;
        var stopWaitHandle = new AutoResetEvent(false);

        // Invoke3LeggedOAuth, and therefor its callback, run on a background thread.
        // Thus, the stopWaitHandle (for main-thread blocking) and exception capture are necessary 
        OAuthHandler.Invoke3LeggedOAuth(clientId, clientSecret, bearer => {
            try {
                if (bearer == null) throw new Exception("Authentication was denied or failed. Please try again.");
                _accessToken = bearer.AccessToken;
                _expiresAt = DateTime.UtcNow.AddSeconds(double.Parse(bearer.ExpiresIn.ToString()));
            } catch (Exception ex) {
                asyncException = ex;
            } finally {
                _ = stopWaitHandle.Set();
            }
        });

        _ = stopWaitHandle.WaitOne(); // Block main thread until async call finishes

        // Now we're back on the main thread, we can safely throw
        if (asyncException != null)
            throw asyncException;
    }
}