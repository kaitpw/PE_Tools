using PeServices.Storage;

namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Orchestrates parameter collection and aggregation across multiple families.
/// </summary>
public class FamilyParamAggregator
{
    private readonly IFamilyParamCollector _collector;

    public FamilyParamAggregator(IFamilyParamCollector collector)
    {
        this._collector = collector;
    }

    /// <summary>
    ///     Aggregates parameter data from all provided families.
    /// </summary>
    /// <param name="doc">The Revit document</param>
    /// <param name="families">Families to analyze</param>
    /// <returns>Dictionary of parameter name to aggregated data</returns>
    public Dictionary<string, AggregatedParamData> Aggregate(Document doc, List<Family> families)
    {
        var aggregated = new Dictionary<string, AggregatedParamData>();

        foreach (var family in families)
        {
            var familyName = family.Name;
            List<ParamCollectionResult> paramResults;

            try
            {
                paramResults = this._collector.CollectParams(doc, family);
            }
            catch
            {
                // Skip families that fail to process
                continue;
            }

            foreach (var param in paramResults)
            {
                var key = GenerateKey(param);

                if (!aggregated.TryGetValue(key, out var existing))
                {
                    existing = new AggregatedParamData
                    {
                        ParamName = param.ParamName,
                        DataTypeLabel = GetDataTypeLabel(param.DataType),
                        ForgeTypeId = param.DataType?.TypeId ?? string.Empty,
                        IsInstance = param.IsInstance,
                        StorageType = param.StorageType.ToString(),
                        IsBuiltIn = param.IsBuiltIn,
                        SharedGuid = param.SharedGuid?.ToString() ?? string.Empty,
                        FamilyNames = []
                    };
                    aggregated[key] = existing;
                }

                if (!existing.FamilyNames.Contains(familyName))
                {
                    existing.FamilyNames.Add(familyName);
                    existing.FamilyCount = existing.FamilyNames.Count;
                }
            }
        }

        return aggregated;
    }

    /// <summary>
    ///     Writes aggregated data to CSV file.
    /// </summary>
    public string WriteToCsv(Dictionary<string, AggregatedParamData> data, Storage storage)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"param-aggregation_{timestamp}.csv";
        var filePath = Path.Combine(storage.OutputDir().DirectoryPath, filename);

        var lines = new List<string> {
            // Header
            "ParamName,IsInstance,FamilyCount,DataTypeLabel,ForgeTypeId,StorageType,IsBuiltIn,SharedGuid,FamilyNames"
        };

        // Sort by family count descending, then by name
        var sortedData = data.Values
            .OrderByDescending(d => d.FamilyCount)
            .ThenBy(d => d.ParamName);

        foreach (var item in sortedData)
        {
            var familyNamesEscaped = EscapeCsvField(string.Join("; ", item.FamilyNames));
            var line = string.Join(",",
                EscapeCsvField(item.ParamName),
                item.IsInstance,
                item.FamilyCount,
                EscapeCsvField(item.DataTypeLabel),
                EscapeCsvField(item.ForgeTypeId),
                item.StorageType,
                item.IsBuiltIn,
                item.SharedGuid,
                familyNamesEscaped
            );
            lines.Add(line);
        }

        File.WriteAllLines(filePath, lines);
        return filePath;
    }

    /// <summary>
    ///     Generates a unique key for a parameter based on name and instance/type distinction.
    /// </summary>
    private static string GenerateKey(ParamCollectionResult param)
    {
        var instanceMarker = param.IsInstance ? "INST" : "TYPE";
        return $"{param.ParamName}|{instanceMarker}";
    }

    /// <summary>
    ///     Gets human-readable label for a data type ForgeTypeId.
    /// </summary>
    private static string GetDataTypeLabel(ForgeTypeId dataType)
    {
        if (dataType == null || string.IsNullOrEmpty(dataType.TypeId))
            return "Unknown";

        try
        {
            return LabelUtils.GetLabelForSpec(dataType);
        }
        catch
        {
            // Fallback to TypeId if label lookup fails
            return dataType.TypeId;
        }
    }

    /// <summary>
    ///     Escapes a field for CSV output (handles commas and quotes).
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
