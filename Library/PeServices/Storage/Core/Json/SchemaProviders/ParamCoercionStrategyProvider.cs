using PeExtensions.FamDocument.SetValue;
using PeServices.Storage.Core.Json.SchemaProcessors;

namespace PeServices.Storage.Core.Json.SchemaProviders;

/// <summary>
///     Provides available ParamCoercionStrategy names for schema generation.
/// </summary>
public class ParamCoercionStrategyProvider : IOptionsProvider {
    public IEnumerable<string> GetExamples() => ParamCoercionStrategyRegistry.GetAllNames();
}