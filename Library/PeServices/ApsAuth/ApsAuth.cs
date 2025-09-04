using PeRevitUI;
using System.Runtime.CompilerServices;

namespace PeServices;

public class ApsAuth {
    private static string? _accessToken;
    private static DateTime _expiresAt;

    private ApsAuth(string clientId, string clientSecret) =>
        _ = new OAuthHandler(clientId, clientSecret);

    public static Result<ApsAuth> Login(string clientId, string clientSecret) {
        try {
            var token = string.Empty;
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                return new Exception("ClientId or ClientSecret is not set");
            var auth = new ApsAuth(clientId, clientSecret);
            auth.GetToken();
            return auth;
        } catch (Exception ex) {
            new Balloon().AddDebug(Balloon.Log.ERR, new StackFrame(), $"Access denied because:\n {ex.Message}").Show();
            return ex;
        }
    }

    private void RefreshToken(ApsAuth auth) {
        var stopWaitHandle = new AutoResetEvent(false); // Allows to sleep thread until 3L access_token received
        // TODO: look at this again, why don't I ever see these TaskDialogs???
        OAuthHandler.Invoke3LeggedOAuth(async void (bearer) => {
            try {
                if (bearer == null) throw new Exception("Authentication failed. Bearer is null");
                _accessToken = bearer.AccessToken;
                _expiresAt = DateTime.UtcNow.AddSeconds(double.Parse(bearer.ExpiresIn.ToString()));
                // tODO: delete at some point
                var profileApi = await OAuthHandler.AuthenticationClient.GetUserInfoAsync(_accessToken);
                _ = TaskDialog.Show("Login Response", $"Hello {profileApi.Name} !!, You are Logged in!");
                _ = stopWaitHandle.Set();
            }
            catch (Exception ex) {
                throw ex; // TODO: idk what to do with this
            }
        });
        _ = stopWaitHandle.WaitOne();
    }

    public string GetToken() {
        if (_accessToken == null || DateTime.UtcNow >= _expiresAt)
            this.RefreshToken(this);
        return _accessToken!;
    }
}