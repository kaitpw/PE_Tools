using System.Net.Http;
using System.Text.Json;

namespace PeServices.Aps.Models;

public static class Hubs {
    private const string BaseUrl = "https://developer.api.autodesk.com/";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<HubsApi.Hubs> GetHubs(string token) {
        var client = new HttpClient();
        //Filter by extension type. hubs:autodesk.bim360:Account (BIM 360 Docs accounts)
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}project/v1/hubs?filter[extension.type]=hubs:autodesk.bim360:Account");
        request.Headers.Add("Authorization", "Bearer " + token);
        var response = await client.SendAsync(request);

        if (response.IsSuccessStatusCode) {
            var jsonContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<HubsApi.Hubs>(jsonContent, JsonOptions) ?? new HubsApi.Hubs();
        }

        return new HubsApi.Hubs();
    }
}