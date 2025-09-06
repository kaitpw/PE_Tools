using Newtonsoft.Json;
using PeServices.Aps.Models;
using System.Net.Http;
using System.Net.Http.Headers;

namespace PeServices.Aps;

public class Parameters {
    private const string BaseUrl = "https://developer.api.autodesk.com/";


    public async Task<ParametersApi.Groups> GetGroups(string hubId, string token) {
        var accountId = hubId.Replace("b.", "");
        var client = new HttpClient { BaseAddress = new Uri($"{BaseUrl}") };
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("parameters/v1/accounts/" + accountId + "/groups");
        return response.IsSuccessStatusCode
            ? JsonConvert.DeserializeObject<ParametersApi.Groups>(await response.Content.ReadAsStringAsync())
            : new ParametersApi.Groups();
    }

    public async Task<ParametersApi.Collections> GetCollections(string hubId, string groupId, string token) {
        var accountId = hubId.Replace("b.", "");
        var gpId = groupId.Replace("b.", "").Replace("-", "");

        var client = new HttpClient { BaseAddress = new Uri($"{BaseUrl}") };
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("parameters/v1/accounts/" + accountId + "/groups/" + gpId +
                                             "/collections?offset=0&limit=10");
        return response.IsSuccessStatusCode
            ? JsonConvert.DeserializeObject<ParametersApi.Collections>(await response.Content.ReadAsStringAsync())
            : new ParametersApi.Collections();
    }

    public async Task<ParametersApi.Parameters> GetParameters(
        string accountId,
        string groupId,
        string collectionId,
        string token
    ) {
        var accId = accountId.Replace("b.", "");
        var gpId = groupId.Replace("b.", "").Replace("-", "");
        var colId = collectionId.Replace("b.", "").Replace("-", "");

        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}parameters/v1/accounts/" + accId + "/groups/" + gpId +
            "/collections/" + colId + "/parameters");
        request.Headers.Add("Authorization", "Bearer " + token);
        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
            return JsonConvert.DeserializeObject<ParametersApi.Parameters>(await response.Content.ReadAsStringAsync());
        return new ParametersApi.Parameters();
    }


    public async Task<bool> CreateParameter(string accountId, string collectionId, string parameterName, string token) {
        var accId = accountId.Replace("b.", "");
        var gpId = accountId.Replace("b.", "");
        var colId = collectionId.Replace("b.", "");

        var paramData = new[] {
            new {
                id = Guid.NewGuid().ToString("N"),
                name = parameterName,
                dataTypeId = "autodesk.spec.string:url-2.0.0",
                readOnly = false
            }
        };

        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{BaseUrl}parameters/v1/accounts/" + accId + "/groups/" + gpId + "/collections/" +
            colId + "/parameters");

        request.Headers.Add("Authorization", "Bearer " + token);

        var jsonStr = JsonConvert.SerializeObject(paramData);
        var content = new StringContent(jsonStr, null, "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        _ = response.EnsureSuccessStatusCode();
        return response.IsSuccessStatusCode;
    }
}