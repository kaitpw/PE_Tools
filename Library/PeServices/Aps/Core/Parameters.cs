using PeServices.Aps.Models;
using PeServices.Storage.Core;
using System.Net.Http;
using System.Text.Json;

namespace PeServices.Aps.Core;

public class Parameters(HttpClient httpClient, TokenProviders.IParameters tokenProvider) {
    private const string Suffix = "parameters/v1/accounts/";
    private readonly TokenProviders.IParameters _tokenProvider = tokenProvider;

    private static async Task<T> DeserializeToType<T>(HttpResponseMessage res) =>
        JsonSerializer.Deserialize<T>(
            await res.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

    private static string Clean(string v) => v.Replace("b.", "").Replace("-", "");

    public async Task<ParametersApi.Groups> GetGroups() {
        var hubId = tokenProvider.GetAccountId();
        var response = await httpClient.GetAsync(Suffix + Clean(hubId) + "/groups");
        return response.IsSuccessStatusCode
            ? await DeserializeToType<ParametersApi.Groups>(response)
            : new ParametersApi.Groups();
    }

    public async Task<ParametersApi.Collections> GetCollections() {
        var (hubId, grpId) = (tokenProvider.GetAccountId(), tokenProvider.GetGroupId());
        var response = await httpClient.GetAsync(
            Suffix + Clean(hubId) + "/groups/" + Clean(grpId) + "/collections?offset=0&limit=10"
        );
        return response.IsSuccessStatusCode
            ? await DeserializeToType<ParametersApi.Collections>(response)
            : new ParametersApi.Collections();
    }

    public async Task<ParametersApi.Parameters> GetParameters(
        JsonReadWriter<ParametersApi.Parameters> cache = null,
        int invalidateCacheAfterMinutes = 10
    ) {
        if (cache is not null) {
            var cacheData = cache.Read();
            // check if cache is old, or if it was just created by initializing the Json instance
            var invalidCache = File.GetLastWriteTime(cache.FilePath) <
                               DateTime.Now.AddMinutes(-invalidateCacheAfterMinutes);
            if (!invalidCache && cacheData.Results.Count > 1) return cacheData;
        }

        var (hubId, grpId, colId) = (tokenProvider.GetAccountId(), tokenProvider.GetGroupId(),
            tokenProvider.GetCollectionId());
        var response = await httpClient.GetAsync(
            Suffix + Clean(hubId) + "/groups/" + Clean(grpId) + "/collections/" + Clean(colId) + "/parameters"
        );
        var deserializedResponse = await DeserializeToType<ParametersApi.Parameters>(response);
        cache?.Write(deserializedResponse);
        return deserializedResponse;
    }

    public Result<SharedParameterElement>[] DownloadParameters(
        Document famDoc,
        ParametersApi.Parameters parameters
    ) {
        var downloadedParams = new List<Result<SharedParameterElement>>();
        if (parameters is { Results: null }) {
            downloadedParams.Add(new Exception("No Parameters Service parameters were found"));
            return downloadedParams.ToArray();
        }

        foreach (var p in parameters.Results) {
            // if (!p.Name.Contains("Manufacturer")) continue;
            var parameterTypeId = new ForgeTypeId(p.Id);
            var downloadOptions = ParameterUtils.DownloadParameterOptions(parameterTypeId);
            if (downloadOptions.GetCategories().Count == 0) {
                var owner = famDoc.OwnerFamily;
                var familyCategory = owner.FamilyCategoryId;
                if (familyCategory != null) {
                    var familyCategorySet = new HashSet<ElementId> { familyCategory };
                    downloadOptions.SetCategories(familyCategorySet);
                }
            } // TODO: workout other defaults if needed.

            var sharedParam = ParameterUtils.DownloadParameter(famDoc, downloadOptions, parameterTypeId);
            downloadedParams.Add(sharedParam);
        }

        return downloadedParams.ToArray();
    }
}