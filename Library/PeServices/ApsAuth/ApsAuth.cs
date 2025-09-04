using Newtonsoft.Json;
using PeRevitUI;
using System.Net.Http;

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
            new Balloon().AddDebug(Balloon.Log.ERR, new StackFrame(), "Access Probably Denied").Show();
            return ex;
        }
    }


    public async Task<string> Get2LeggedForgeToken(string clientId, string clientSecret) {
        var client = new HttpClient { BaseAddress = new Uri("https://developer.api.autodesk.com") };
        var request = new HttpRequestMessage(HttpMethod.Post, "/authentication/v1/authenticate");
        var credentials = new Dictionary<string, string> {
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "grant_type", "client_credentials" },
            { "scope", "code:all data:create data:write data:read bucket:create bucket:delete" }
        };

        var content = new FormUrlEncodedContent(credentials);
        request.Content = content;
        var response = await client.SendAsync(request);
        _ = response.EnsureSuccessStatusCode();
        var rep = JsonConvert.DeserializeObject<ITokenModel>(await response.Content.ReadAsStringAsync());
        return rep.access_token;
    }


    public interface ITokenModel {
        string access_token { get; set; }
        string token_type { get; set; }
        int expires_in { get; set; }
    }
}