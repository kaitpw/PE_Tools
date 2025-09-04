using Autodesk.Authentication;
using Autodesk.Authentication.Model;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PeServices;

/// <summary>
///     Defines the <see cref="OAuthHandler" />
/// </summary>
internal class OAuthHandler {
    /// <summary> A delegate to hold the callback function for when 3-legged OAuth completes </summary>
    public delegate void OAuthCallbackDelegate(ThreeLeggedToken? bearer);

    private static string? CLIENT_ID;
    private static string? CLIENT_SECRET;
    private static readonly int PORT = 8080;
    private static readonly string FORGE_CALLBACK = $"http://localhost:{PORT}/api/aps/callback/oauth";

    public static readonly AuthenticationClient AuthenticationClient = new();


    /// <summary> The list of scopes for the 3-legged OAuth 2.0 client. </summary>
    private static readonly List<Scopes> Scopes = [
        Autodesk.Authentication.Model.Scopes.AccountRead,
        Autodesk.Authentication.Model.Scopes.DataCreate,
        Autodesk.Authentication.Model.Scopes.DataWrite,
        Autodesk.Authentication.Model.Scopes.DataRead,
        Autodesk.Authentication.Model.Scopes.BucketRead
    ];

    /// <summary> Initialize the 3-legged oAuth 2.0 client. </summary>
    private static readonly ThreeLeggedToken ThreeLeggedApi = new();

    /// <summary> TCP listener for the OAuth callback to the local machine. Avoid admin privileges required by http listeners</summary>
    private static readonly TcpListener TcpListener = new(IPAddress.Loopback, PORT);

    public OAuthHandler(string clientId, string clientSecret) {
        CLIENT_ID = clientId;
        CLIENT_SECRET = clientSecret;
    }

    /// <summary> Gets or sets the InternalToken.</summary>
    private static dynamic? InternalToken { get; set; }


    /// <summary>
    ///     Get the access token from Autodesk.
    /// </summary>
    /// <param name="scopes">The scopes<see cref="Autodesk.Authentication.Model.Scopes" />.</param>
    /// <returns>The <see cref="Task{dynamic}" />.</returns>
    private static async Task<dynamic> Get2LeggedTokenAsync(List<Scopes> scopes) {
        dynamic bearer = await AuthenticationClient.GetTwoLeggedTokenAsync(CLIENT_ID,
            CLIENT_SECRET,
            scopes);
        return bearer;
    }

    /// <summary>
    ///     The GetInternalAsync.
    /// </summary>
    /// <returns>The <see cref="Task{dynamic}" />.</returns>
    public static async Task<dynamic> GetInternalAsync() {
        if (InternalToken == null || InternalToken?.ExpiresAt < DateTime.UtcNow) {
            InternalToken = await Get2LeggedTokenAsync([
                Autodesk.Authentication.Model.Scopes.BucketCreate,
                Autodesk.Authentication.Model.Scopes.BucketRead,
                Autodesk.Authentication.Model.Scopes.BucketDelete,
                Autodesk.Authentication.Model.Scopes.DataRead,
                Autodesk.Authentication.Model.Scopes.DataWrite,
                Autodesk.Authentication.Model.Scopes.DataCreate,
                Autodesk.Authentication.Model.Scopes.CodeAll
            ]);
            InternalToken.ExpiresAt = DateTime.UtcNow.AddSeconds(InternalToken.expires_in);
        }

        return InternalToken;
    }


    /// <summary>
    ///     The Invoke3LeggedOAuth.
    /// </summary>
    /// <param name="callback">The cb<see cref="OAuthCallbackDelegate" />.</param>
    public static void Invoke3LeggedOAuth(OAuthCallbackDelegate callback) => _3leggedAsync(callback);

    /// <summary>
    ///     The _3leggedAsync.
    /// </summary>
    /// <param name="cb">The cb<see cref="OAuthCallbackDelegate" />.</param>
    private static void _3leggedAsync(OAuthCallbackDelegate cb) {
        try {
            TcpListener.Start();
            // Generate a URL page that asks for permissions for the specified Scopess, and call our default web browser.
            var oauthUrl = AuthenticationClient.Authorize(CLIENT_ID, ResponseType.Code, FORGE_CALLBACK,
                Scopes);

            var startInfo = new ProcessStartInfo(oauthUrl) { UseShellExecute = true };
            _ = Process.Start(startInfo);
            // Wait for the callback on background thread to prevent UI freeze
            _ = Task.Run(() => _3leggedAsyncWaitForCode(cb));
        } catch (Exception ex) {
            Debug.WriteLine($"Error starting TcpListener: {ex.Message}");
            cb?.Invoke(null);
        }
    }


    /// <summary>
    ///     The _3leggedAsyncWaitForCode.
    /// </summary>
    /// <param name="callback">The cb<see cref="OAuthCallbackDelegate" />.</param>
    private static async void _3leggedAsyncWaitForCode(OAuthCallbackDelegate callback) {
        try {
            var client = await TcpListener.AcceptTcpClientAsync();
            var request = ReadString(client);
            Debug.WriteLine($"Received request: {request}");
            var code = ExtractCodeFromRequest(request);
            Debug.WriteLine($"Extracted code: {code}");

            // Send response to browser
            var responseString =
                "<html><body><h2>Login Success</h2><p>You can now close this window!</p></body></html>";
            await WriteStringAsync(client, responseString);
            client.Dispose();

            // Now request the final access_token
            if (!string.IsNullOrEmpty(code)) {
                var bearer =
                    await AuthenticationClient.GetThreeLeggedTokenAsync(CLIENT_ID, code, FORGE_CALLBACK,
                        CLIENT_SECRET);
                callback.Invoke(bearer);
            } else
                callback.Invoke(null);
        } catch (Exception ex) {
            Debug.WriteLine(ex.Message);
            callback.Invoke(null);
        } finally {
            TcpListener?.Stop();
        }
    }

    /// <summary>
    ///     Reads the HTTP request from the TCP client.
    /// </summary>
    /// <param name="client">The TCP client.</param>
    /// <returns>The request string.</returns>
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

    /// <summary>
    ///     Writes the HTTP response to the TCP client.
    /// </summary>
    /// <param name="client">The TCP client.</param>
    /// <param name="str">The response body.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static Task WriteStringAsync(TcpClient client, string str) => Task.Run(() => {
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

    /// <summary>
    ///     Extracts the authorization code from the HTTP request.
    /// </summary>
    /// <param name="request">The HTTP request string.</param>
    /// <returns>The authorization code or null.</returns>
    private static string ExtractCodeFromRequest(string request) {
        var lines = request.Split('\n');
        if (lines.Length > 0) {
            var requestLine = lines[0].Trim();
            if (requestLine.StartsWith("GET ")) {
                var urlPart = requestLine.Split(' ')[1];
                var queryStart = urlPart.IndexOf('?');
                if (queryStart >= 0) {
                    var query = urlPart[(queryStart + 1)..];
                    var parameters = query.Split('&');
                    foreach (var param in parameters) {
                        var kv = param.Split('=');
                        if (kv.Length == 2 && kv[0] == "code") return kv[1];
                    }
                }
            }
        }

        return null;
    }
}