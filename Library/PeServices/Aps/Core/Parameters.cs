using PE_Tools;
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

    public ParameterDownloadResult[] DownloadParameters(
        Document doc,
        List<DlOptsConstructionResult> psParamsDlOptsResultList
    ) {
        var downloadedParams = new List<ParameterDownloadResult>();
        foreach (var listEl in psParamsDlOptsResultList) {
            var psParamInfo = listEl.PsParamInfo;
            var (downloadOpts, err) = listEl.PsParamDlOptsResult;
            try {
                if (err is not null) throw new Exception(err.Message);

                var parameterTypeId = psParamInfo.DownloadOptions.ParameterTypeId;
                var sharedParam = ParameterUtils.DownloadParameter(doc, downloadOpts, parameterTypeId);
                downloadedParams.Add(new ParameterDownloadResult(psParamInfo, sharedParam));
            } catch (Exception ex) {
                if (ex.Message.Contains("Parameter with a matching name") ||
                    ex.Message.Contains("Parameter with a matching GUID"))
                    downloadedParams.Add(new ParameterDownloadResult(psParamInfo, ex));
                else {
                    var unknownException = new Exception("Unknown parameter download exception", ex);
                    downloadedParams.Add(new ParameterDownloadResult(psParamInfo, unknownException));
                }
            }
        }

        return downloadedParams.ToArray();
    }

    public class ParameterDownloadResult(
        ParametersApi.Parameters.ParametersResult originalParameter,
        Result<SharedParameterElement> downloadResult) {
        public ParametersApi.Parameters.ParametersResult OriginalParameter { get; } = originalParameter;
        public Result<SharedParameterElement> DownloadResult { get; } = downloadResult;
    }
}