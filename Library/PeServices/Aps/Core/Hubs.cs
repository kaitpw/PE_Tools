using PeServices.Aps.Models;
using System.Net.Http;
using System.Text.Json;

namespace PeServices.Aps.Core;

public class Hubs(HttpClient httpClient) {
    private static async Task<T> DeserializeToType<T>(HttpResponseMessage res) =>
        JsonSerializer.Deserialize<T>(
            await res.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

    private static string Clean(string v) => v.Replace("b.", "").Replace("-", "");

    public async Task<HubsApi.Hubs> GetHubs() {
        //Filter by extension type. hubs:autodesk.bim360:Account (BIM 360 Docs accounts)
        var response = await httpClient.GetAsync("project/v1/hubs?filter[extension.type]=hubs:autodesk.bim360:Account");
        return response.IsSuccessStatusCode
            ? await DeserializeToType<HubsApi.Hubs>(response)
            : null;
    }
}