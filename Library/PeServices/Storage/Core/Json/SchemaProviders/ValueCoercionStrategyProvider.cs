using PeExtensions.FamDocument.SetValue;
using PeServices.Storage.Core.Json.SchemaProcessors;

namespace PeServices.Storage.Core.Json.SchemaProviders;

/// <summary>
///     Provides available ValueCoercionStrategy names for schema generation.
/// </summary>
public class ValueCoercionStrategyProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() => ValueCoercionStrategyRegistry.GetAllNames();
}