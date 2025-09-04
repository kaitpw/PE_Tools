using PeRevitUI;

namespace PeServices;

public class ApsAuth {
    public static Result<string> Login(string clientId, string clientSecret) {
        try {
            var token = string.Empty;
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                return new Exception("ClientId or ClientSecret is not set");

            _ = new OAuthHandler(clientId, clientSecret);
            var stopWaitHandle = new AutoResetEvent(false); // Allows to sleep thread until 3L access_token received
            // TODO: look at this again, why don't I ever see these TaskDialogs???
            OAuthHandler.Invoke3LeggedOAuth(async bearer => {
                if (bearer == null) {
                    _ = TaskDialog.Show("Login Response", "Sorry, Authentication failed! 3legged test");
                    return;
                }

                token = bearer.AccessToken;
                var dt = DateTime.Now;
                _ = dt.AddSeconds(double.Parse(bearer.ExpiresIn.ToString()));
                var profileApi = await OAuthHandler.AuthenticationClient.GetUserInfoAsync(token);
                _ = TaskDialog.Show("Login Response", $"Hello {profileApi.Name} !!, You are Logged in!");
                _ = stopWaitHandle.Set();
            });
            _ = stopWaitHandle.WaitOne();
            return token;
        } catch (Exception ex) {
            new Balloon().AddDebug(Balloon.Log.ERR, new StackFrame(), $"Access denied because:\n {ex.Message}").Show();
            return ex;
        }
    }
}