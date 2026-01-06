using AddinFamilyFoundrySuite.Core.Aggregators.Snapshots;
using AddinFamilyFoundrySuite.Core.Snapshots;
using PeServices.Storage;

namespace AddinFamilyFoundrySuite.Core.Aggregators;

/// <summary>
///     Orchestrates parameter collection and aggregation across multiple families.
/// </summary>
public static class FamilyParamAggregator {
    /// <summary>
    ///     Aggregates parameter data from all provided families.
    /// </summary>
    /// <returns>Dictionary of parameter name to aggregated data</returns>
    public static IEnumerable<AggregatedParamData> Aggregate(Document doc,
        CollectorQueue collectorQueue,
        List<Family> families) {
        var aggregated = new Dictionary<(string, bool, string), AggregatedParamData>();
        foreach (var family in families) {
            var familyName = family.Name;
            var categoryName = family.FamilyCategory?.Name ?? "Unknown";

            try {
                var snapshot = new FamilySnapshot { FamilyName = familyName };
                collectorQueue.ToProjectCollectorFunc()(snapshot, doc, family);

                foreach (var param in snapshot.Parameters?.Data ?? []) {
                    var key = GenerateKey(param);

                    if (!aggregated.TryGetValue(key, out var existing)) {
                        existing = new AggregatedParamData(param);
                        aggregated[key] = existing;
                    }

                    if (!existing.FamilyNames.Contains(familyName)) existing.FamilyNames.Add(familyName);
                    _ = existing.FamilyCategories.Add(categoryName);
                }
            } catch {
                // Skip families that fail to process
            }
        }

        return aggregated.Values;
    }

    /// <summary>
    ///     Enriches aggregated parameter data with schedule information.
    ///     Looks at all schedules and adds schedule names and categories where each parameter appears.
    /// </summary>
    public static void EnrichWithScheduleData(Document doc,
        IEnumerable<AggregatedParamData> aggregatedData,
        List<Category> categoryFilter = null) {
        var paramToSchedules = new Dictionary<string, List<string>>();
        var paramToScheduleCategories = new Dictionary<string, HashSet<string>>();

        var schedules = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .OfType<ViewSchedule>()
            .Where(s => s.Definition != null);

        if (categoryFilter?.Any() == true)
            schedules = schedules.Where(s => categoryFilter.Any(c => c.Id == s.Definition.CategoryId));

        foreach (var schedule in schedules) {
            var definition = schedule.Definition;
            var scheduleName = schedule.Name;
            var scheduleCategory = doc.GetElement(definition.CategoryId)?.Name ?? "Unknown";

            for (var i = 0; i < definition.GetFieldCount(); i++) {
                var field = definition.GetField(i);
                if (field.ParameterId == ElementId.InvalidElementId) continue;

                var paramElement = doc.GetElement(field.ParameterId) as ParameterElement;
                var paramName = paramElement?.Name ?? field.GetName();

                if (!paramToSchedules.TryGetValue(paramName, out var scheduleList)) {
                    scheduleList = [];
                    paramToSchedules[paramName] = scheduleList;
                }

                if (!scheduleList.Contains(scheduleName)) scheduleList.Add(scheduleName);

                if (!paramToScheduleCategories.TryGetValue(paramName, out var categorySet)) {
                    categorySet = [];
                    paramToScheduleCategories[paramName] = categorySet;
                }

                _ = categorySet.Add(scheduleCategory);
            }
        }

        foreach (var data in aggregatedData) {
            if (paramToSchedules.TryGetValue(data.ParamName, out var scheduleNames))
                data.ScheduleNames = scheduleNames;

            if (paramToScheduleCategories.TryGetValue(data.ParamName, out var scheduleCategories))
                data.ScheduleCategories = scheduleCategories;
        }
    }

    /// <summary>
    ///     Writes aggregated data to CSV file.
    /// </summary>
    public static string WriteToCsv(IEnumerable<AggregatedParamData> data, Storage storage) {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var filename = $"param-aggregation_{timestamp}.csv";
        var filePath = Path.Combine(storage.OutputDir().DirectoryPath, filename);

        var lines = new List<string> {
            // Header
            "ParamName,FamilyCount,ScheduleCount,IsInstance,IsBuiltIn,IsProjectParameter,IsFamilyParameter,SharedGuid,StorageType,DataType,DataTypeId,FamilyCategories,ScheduleCategories,FamilyNames,ScheduleNames"
        };
        lines.AddRange(from item in data
                       let familyNamesEscaped = EscapeCsvField(string.Join("; ", item.FamilyNames))
                       let scheduleNamesEscaped = EscapeCsvField(string.Join("; ", item.ScheduleNames))
                       let familyCategoriesEscaped = EscapeCsvField(string.Join("; ", item.FamilyCategories.OrderBy(c => c)))
                       let scheduleCategoriesEscaped = EscapeCsvField(string.Join("; ", item.ScheduleCategories.OrderBy(c => c)))
                       select string.Join(",",
                           EscapeCsvField(item.ParamName),
                           item.FamilyCount,
                           item.ScheduleCount,
                           item.IsInstance,
                           item.IsBuiltIn,
                           item.IsProjectParameter,
                           item.IsFamilyParameter,
                           item.SharedGuid,
                           item.StorageType,
                           EscapeCsvField(item.DataType),
                           EscapeCsvField(item.DataTypeId),
                           familyCategoriesEscaped,
                           scheduleCategoriesEscaped,
                           familyNamesEscaped,
                           scheduleNamesEscaped));

        File.WriteAllLines(filePath, lines);
        return filePath;
    }

    /// <summary>
    ///     Generates a unique key for a parameter based on name and instance/type distinction.
    /// </summary>
    private static (string, bool, string) GenerateKey(ParamSnapshot param) =>
        (param.Name, param.IsInstance, param.SharedGuid?.ToString() ?? string.Empty);

    /// <summary>
    ///     Escapes a field for CSV output (handles commas and quotes).
    /// </summary>
    private static string EscapeCsvField(string field) {
        if (string.IsNullOrEmpty(field)) return string.Empty;

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            return $"\"{field.Replace("\"", "\"\"")}\"";

        return field;
    }
}