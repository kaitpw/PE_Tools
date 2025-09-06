using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
namespace PeServices.Aps.Models;


public static class Hubs {
    const string BaseUrl = "https://developer.api.autodesk.com/";
    
    public static async Task<HubsApi.Hubs> GetHubs(string token) {
        var client = new HttpClient();
        //Filter by extension type. hubs:autodesk.bim360:Account (BIM 360 Docs accounts)
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}project/v1/hubs?filter[extension.type]=hubs:autodesk.bim360:Account");
        request.Headers.Add("Authorization", "Bearer " + token);
        var response = await client.SendAsync(request);
        
        if (response.IsSuccessStatusCode) {
            var jsonContent = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<HubsApi.Hubs>(jsonContent, options) ?? new HubsApi.Hubs();
        }

        return new HubsApi.Hubs();
    }
}