using Autodesk.Authentication;
using Autodesk.Authentication.Model;
using PeRevitUI;
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

    private const int Port = 8080;

    private static string? _clientId;
    private static string? _clientSecret;
    private static readonly string ForgeCallback = $"http://localhost:{Port}/api/aps/callback/oauth";

    public static readonly AuthenticationClient AuthenticationClient = new();

    /// <summary> TCP listener for the OAuth callback to the local machine. Avoid admin privileges required by http listeners</summary>
    private static readonly TcpListener TcpListener = new(IPAddress.Loopback, Port);

    private static readonly List<Scopes> OAuthClientScopes = [
        Scopes.AccountRead, Scopes.DataCreate, Scopes.DataWrite, Scopes.DataRead, Scopes.BucketRead
    ];

    public OAuthHandler(string clientId, string clientSecret) {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public static void Invoke3LeggedOAuth(OAuthCallbackDelegate callback) => _3leggedAsync(callback);

    /// <summary>
    ///     Generate a URL page that asks for permissions for the specified Scopes, and call our default web browser
    /// </summary>
    private static void _3leggedAsync(OAuthCallbackDelegate cb) {
        try {
            TcpListener.Start();
            var oauthUrl = AuthenticationClient.Authorize(_clientId, ResponseType.Code, ForgeCallback,
                OAuthClientScopes);
            _ = Process.Start(new ProcessStartInfo(oauthUrl) { UseShellExecute = true });
            // Wait for the callback on background thread to prevent UI freeze
            _ = Task.Run(() => _3leggedAsyncWaitForCode(cb));
        } catch (Exception ex) {
            new Balloon().Add(Balloon.Log.ERR, new StackFrame(), $"Error starting TcpListener: {ex.Message}").Show();
            cb?.Invoke(null);
        }
    }


    /// <summary>
    ///     The _3leggedAsyncWaitForCode.
    /// </summary>
    private static async void _3leggedAsyncWaitForCode(OAuthCallbackDelegate callback) {
        try {
            var client = await TcpListener.AcceptTcpClientAsync();
            var request = ReadString(client);
            var code = ExtractCodeFromRequest(request);
            await WriteSuccessStringAsync(client);
            client.Dispose();

            // Now request the final access_token
            if (!string.IsNullOrEmpty(code)) {
                var bearer =
                    await AuthenticationClient.GetThreeLeggedTokenAsync(_clientId, code, ForgeCallback,
                        _clientSecret);
                callback.Invoke(bearer);
            } else
                callback.Invoke(null);
        } catch (Exception ex) {
            new Balloon().Add(Balloon.Log.ERR, new StackFrame(), $"Error accepting request: {ex.Message}").Show();
            callback.Invoke(null);
        } finally {
            TcpListener?.Stop();
        }
    }

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

    private static Task WriteSuccessStringAsync(TcpClient client) => Task.Run(() => {
        const string str = """
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
}