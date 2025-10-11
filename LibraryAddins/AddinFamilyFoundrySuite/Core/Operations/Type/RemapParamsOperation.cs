using AddinFamilyFoundrySuite.Core.Settings;

namespace AddinFamilyFoundrySuite.Core.Operations.Type;

public static class RemapParamsOperation {
    /// <summary>
    ///     Per-type remap method for use with the new fluent API
    /// </summary>
    public static void RemapParameters(this Document famDoc, List<ParamsRemap.RemapDataRecord> paramRemaps) {
        foreach (var p in paramRemaps) {
            try {
                _ = famDoc.MapValue(p.CurrNameOrId, p.NewNameOrId, p.MappingPolicy);
            } catch (Exception ex) {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}