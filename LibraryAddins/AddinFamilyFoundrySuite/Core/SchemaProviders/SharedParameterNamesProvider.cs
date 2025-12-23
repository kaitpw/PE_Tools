using Newtonsoft.Json;
using PeServices.Aps.Models;
using PeServices.Storage;
using PeServices.Storage.Core.Json.SchemaProcessors;

namespace AddinFamilyFoundrySuite.Core.SchemaProviders;

/// <summary>
///     Provides shared parameter names from the APS cache for JSON schema examples.
///     Used to enable LSP autocomplete for parameter name properties.
/// </summary>
public class SharedParameterNamesProvider : ISchemaExamplesProvider {
    private const string CacheFilename = "parameters-service-cache";

    public IEnumerable<string> GetExamples() {
        try {
            var cache = Storage.GlobalDir().StateJson<ParametersApi.Parameters>(CacheFilename)
                as PeServices.Storage.Core.JsonReader<ParametersApi.Parameters>;
            if (!File.Exists(cache.FilePath)) return [];
            return cache.Read().Results?.Select(p => p.Name) ?? [];
        } catch {
            // Cache missing or invalid - no examples, no crash
            return [];
        }
    }
}

