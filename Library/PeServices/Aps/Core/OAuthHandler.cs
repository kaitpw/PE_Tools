using Autodesk.Authentication;
using Autodesk.Authentication.Model;
using Nice3point.Revit.Extensions;
using PeRevit.Ui;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PeServices.Aps.Core;

/// <summary>
///     Fully static class to handle Oauth flow. <see cref="Invoke3LeggedOAuth" /> will open the users
///     default browser to give permissions to this app. Upon approval <see cref="CallbackDelegate" />
///     will receive the bearer token.
/// </summary>
/// <remarks>
///     Uses a TCP listener rather than HTTP in order to sidestep need for admin privileges.
/// </remarks>
internal static class OAuthHandler {
    /// <summary> A delegate to hold the callback function for when 3-legged OAuth completes </summary>
    public delegate void CallbackDelegate(ThreeLeggedToken bearer);

    private const int Port = 8080;
    private static readonly string ForgeCallback = $"http://localhost:{Port}/api/aps/callback/oauth";
    private static readonly AuthenticationClient AuthenticationClient = new();
    private static readonly TcpListener TcpListener = new(IPAddress.Loopback, Port);
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    private static readonly List<Scopes> OAuthClientScopes = [
        Scopes.AccountRead, Scopes.DataCreate, Scopes.DataWrite, Scopes.DataRead, Scopes.BucketRead
    ];

    public static void Invoke3LeggedOAuth(string clientId, string clientSecret, CallbackDelegate callback) {
        var oAuthData = new OAuthData(clientId, clientSecret,
            string.IsNullOrEmpty(clientSecret) ? GenerateRandomString() : null);

        async Task<ThreeLeggedToken> GetToken(string code) {
            return await Get3LeggedToken(oAuthData, code);
        }

        _Async3LegOAuth(GenerateOAuthUrl(oAuthData), GetToken, callback);
    }

    private static void _Async3LegOAuth(string oAuthUrl, Get3LegTokenDelegate getToken, CallbackDelegate cb) {
        try {
            TcpListener.Stop(); // Ensure any previous listener is stopped
            TcpListener.Start();
            if (!((IPEndPoint)TcpListener.LocalEndpoint).Port.Equals(Port))
                throw new Exception($"Failed to start TCP listener on port {Port}");
            _ = Process.Start(new ProcessStartInfo(oAuthUrl) { UseShellExecute = true });
            _ = Task.Run(async () => {
                try {
                    var client = await TcpListener.AcceptTcpClientAsync();
                    var request = ReadString(client);
                    var code = ExtractCodeFromRequest(request);

                    await WriteSuccessStringAsync(client,
                        code.IsNullOrEmpty() ? CallbackPages.ErrorPage : CallbackPages.SuccessPage);
                    client.Dispose();

                    if (!string.IsNullOrEmpty(code)) {
                        var bearer = await getToken(code);
                        cb?.Invoke(bearer);
                    } else
                        cb?.Invoke(null);
                } catch (Exception ex) {
                    new Balloon().Add(new StackFrame(), Log.ERR, $"Error in OAuth flow: {ex.Message}").Show();
                    cb?.Invoke(null);
                } finally {
                    TcpListener?.Stop();
                }
            });
        } catch (Exception ex) {
            new Balloon().Add(new StackFrame(), ex).Show();
            cb?.Invoke(null);
        }
    }

    private static string GenerateOAuthUrl(OAuthData d) =>
        d.IsNormalFlow()
            ? AuthenticationClient.Authorize(d.ClientId, ResponseType.Code, ForgeCallback, OAuthClientScopes)
            : d.IsPkceFlow()
                ? AuthenticationClient.Authorize(d.ClientId, ResponseType.Code, ForgeCallback, OAuthClientScopes,
                    codeChallenge: GenerateCodeChallenge(d.CodeVerifier), codeChallengeMethod: "S256",
                    nonce: GenerateRandomString())
                : null;

    private static async Task<ThreeLeggedToken> Get3LeggedToken(OAuthData d, string code) =>
        d.IsNormalFlow()
            ? await AuthenticationClient.GetThreeLeggedTokenAsync(d.ClientId, code, ForgeCallback,
                d.ClientSecret)
            : d.IsPkceFlow()
                ? await AuthenticationClient.GetThreeLeggedTokenAsync(d.ClientId, code, ForgeCallback,
                    codeVerifier: d.CodeVerifier)
                : null;


    private static string ReadString(TcpClient client) {
        var readBuffer = new byte[client.ReceiveBufferSize];
        using var inStream = new MemoryStream();
        var stream = client.GetStream();
        while (stream.DataAvailable) {
            var numberOfBytesRead = stream.Read(readBuffer, 0, readBuffer.Length);
            if (numberOfBytesRead <= 0) break;
            inStream.Write(readBuffer, 0, numberOfBytesRead);
        }

        return Encoding.UTF8.GetString(inStream.ToArray());
    }

    private static Task WriteSuccessStringAsync(TcpClient client, string str) => Task.Run(() => {
        using var writer = new StreamWriter(client.GetStream(), new UTF8Encoding(false));
        writer.Write("HTTP/1.0 200 OK");
        writer.Write(Environment.NewLine);
        writer.Write("Content-Type: text/html; charset=UTF-8");
        writer.Write(Environment.NewLine);
        writer.Write("Content-Length: " + str.Length);
        writer.Write(Environment.NewLine);
        writer.Write("Connection: close");
        writer.Write(Environment.NewLine);
        writer.Write(Environment.NewLine);
        writer.Write(str);
        writer.Flush();
    });

    private static string ExtractCodeFromRequest(string request) {
        var lines = request.Split('\n');
        if (lines.Length <= 0) return null;
        var requestLine = lines[0].Trim();
        if (!requestLine.StartsWith("GET ")) return null;
        var urlPart = requestLine.Split(' ')[1];
        var queryStart = urlPart.IndexOf('?');
        if (queryStart < 0) return null;
        var query = urlPart[(queryStart + 1)..];
        var parameters = query.Split('&');
        return (from param in parameters
            select param.Split('=')
            into kv
            where kv.Length == 2 && kv[0] == "code"
            select kv[1]).FirstOrDefault();
    }


    private static string GenerateRandomString() {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        var bytes = new byte[128];
        Rng.GetBytes(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static string GenerateCodeChallenge(string codeVerifier) {
        var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var b64Hash = Convert.ToBase64String(hash);
        var code = Regex.Replace(b64Hash, "\\+", "-");
        code = Regex.Replace(code, "\\/", "_");
        code = Regex.Replace(code, "=+$", "");
        return code;
    }

    private class OAuthData {
        public readonly string ClientId;
        public readonly string ClientSecret;
        public readonly string CodeVerifier;

        public OAuthData(string clientId, string clientSecret, string codeVerifier) {
            var hasClientSecret = !string.IsNullOrEmpty(clientSecret);
            var hasCodeVerifier = !string.IsNullOrEmpty(codeVerifier);

            if (string.IsNullOrEmpty(clientId)) throw new Exception("ClientId is not set.");
            this.ClientId = clientId;

            if (hasClientSecret && !hasCodeVerifier) {
                this.ClientSecret = clientSecret;
                this.CodeVerifier = null;
            } else if (!hasClientSecret && hasCodeVerifier) {
                this.ClientSecret = null;
                this.CodeVerifier = codeVerifier;
            } else {
                var emptyValue = string.Empty;
                if (!hasClientSecret) emptyValue = "ClientSecret";
                if (!hasCodeVerifier) emptyValue = "CodeVerifier";
                throw new Exception($"{emptyValue} is not set.");
            }
        }

        public bool IsNormalFlow() => this.ClientSecret != null && this.CodeVerifier == null;
        public bool IsPkceFlow() => this.ClientSecret == null && this.CodeVerifier != null;
    }

    private static class CallbackPages {
        public const string SuccessPage = """
                                          <html>
                                            <head>
                                              <title>Login Status</title>
                                              <style>
                                                body {
                                                  font-family: Arial, Helvetica, sans-serif;
                                                  display: flex;
                                                  flex-direction: column;
                                                  justify-content: center;
                                                  align-items: center;
                                                  min-height: 100vh; /* Ensures the body takes at least the full viewport height */
                                                  margin: 0; /* Remove default body margin */
                                                }
                                              </style>
                                            </head>
                                            <body>
                                              <h2>Login Success</h2>
                                              <p>You can now close this window!</p>
                                            </body>
                                          </html>
                                          """;

        public const string ErrorPage = """
                                        <html>
                                          <head>
                                            <title>Login Status</title>
                                            <style>
                                              body {
                                                font-family: Arial, Helvetica, sans-serif;
                                                display: flex;
                                                flex-direction: column;
                                                justify-content: center;
                                                align-items: center;
                                                min-height: 100vh; /* Ensures the body takes at least the full viewport height */
                                                margin: 0; /* Remove default body margin */
                                              }
                                            </style>
                                          </head>
                                          <body>
                                            <h2>Login Failed</h2>
                                            <p>Please try again.</p>
                                          </body>
                                        </html>
                                        """;
    }

    private delegate Task<ThreeLeggedToken> Get3LegTokenDelegate(string code);
}