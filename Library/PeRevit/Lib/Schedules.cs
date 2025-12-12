using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PeRevit.Lib;

public class ScheduleSpec {
    public string Name { get; set; }
    public string CategoryName { get; set; }
    public bool IsItemized { get; set; } = true;
    public List<ScheduleFieldSpec> Fields { get; set; } = [];
    public List<ScheduleSortGroupSpec> SortGroup { get; set; } = [];
}

public class ScheduleFieldSpec {
    public required string ParameterName { get; set; }
    public string ColumnHeaderOverride { get; set; }
    public string HeaderGroup { get; set; }
    public bool IsHidden { get; set; }
    public FieldDisplayType DisplayType { get; set; } = FieldDisplayType.Standard;

    /// <summary>
    ///     Column width on sheet in feet. Null uses default width.
    /// </summary>
    public double? ColumnWidth { get; set; }

    /// <summary>
    ///     For calculated fields only. Indicates this is a formula or percentage field.
    ///     Note: Formula strings cannot be read/written via Revit API - only the field type is preserved.
    /// </summary>
    public CalculatedFieldType? CalculatedType { get; set; }

    /// <summary>
    ///     For Percentage calculated fields only. The name of the field to calculate percentages of.
    /// </summary>
    public string PercentageOfField { get; set; }
}

/// <summary>
///     Maps to Revit's ScheduleFieldDisplayType enum (Formatting tab calculation options)
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum FieldDisplayType {
    Standard = 0,
    Totals = 1,
    MinAndMax = 2,
    Maximum = 3,
    Minimum = 4
}

/// <summary>
///     Type of calculated field
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum CalculatedFieldType {
    Formula,
    Percentage
}

[JsonConverter(typeof(StringEnumConverter))]
public enum ScheduleSortOrder {
    Ascending,
    Descending
}

public class ScheduleSortGroupSpec {
    public required string FieldName { get; set; }
    public ScheduleSortOrder SortOrder { get; set; } = ScheduleSortOrder.Ascending;
    public bool ShowHeader { get; set; }
    public bool ShowFooter { get; set; }
    public bool ShowBlankLine { get; set; }
}

public static class ScheduleHelper {
    public static ScheduleSpec SerializeSchedule(ViewSchedule schedule) {
        var def = schedule.Definition;
        var category = Category.GetCategory(schedule.Document, def.CategoryId);
        var categoryName = category?.Name ?? string.Empty;

        var spec = new ScheduleSpec {
            Name = schedule.Name,
            CategoryName = categoryName,
            IsItemized = def.IsItemized,
            Fields = [],
            SortGroup = []
        };

        // Serialize fields
        for (var i = 0; i < def.GetFieldCount(); i++) {
            var field = def.GetField(i);
            var fieldName = field.GetName();

            var fieldSpec = new ScheduleFieldSpec {
                ParameterName = fieldName,
                ColumnHeaderOverride = field.ColumnHeading != fieldName ? field.ColumnHeading : null,
                IsHidden = field.IsHidden,
                DisplayType = (FieldDisplayType)(int)field.DisplayType,
                ColumnWidth = field.SheetColumnWidth
            };

            // Handle calculated fields
            if (field.IsCalculatedField) {
                fieldSpec.CalculatedType = field.FieldType == ScheduleFieldType.Formula
                    ? CalculatedFieldType.Formula
                    : CalculatedFieldType.Percentage;

                // For percentage fields, capture the field it's based on
                if (field.FieldType == ScheduleFieldType.Percentage) {
                    var percentageOfId = field.PercentageOf;
                    if (percentageOfId != null && def.IsValidFieldId(percentageOfId)) {
                        var percentageOfField = def.GetField(percentageOfId);
                        fieldSpec.PercentageOfField = percentageOfField.GetName();
                    }
                }
            }

            spec.Fields.Add(fieldSpec);
        }

        // Serialize sort/group fields
        for (var i = 0; i < def.GetSortGroupFieldCount(); i++) {
            var sortGroupField = def.GetSortGroupField(i);
            var field = def.GetField(sortGroupField.FieldId);
            var fieldName = field.GetName();

            var sortGroupSpec = new ScheduleSortGroupSpec {
                FieldName = fieldName,
                SortOrder = sortGroupField.SortOrder == Autodesk.Revit.DB.ScheduleSortOrder.Ascending
                    ? ScheduleSortOrder.Ascending
                    : ScheduleSortOrder.Descending,
                ShowHeader = sortGroupField.ShowHeader,
                ShowFooter = sortGroupField.ShowFooter,
                ShowBlankLine = sortGroupField.ShowBlankLine
            };

            spec.SortGroup.Add(sortGroupSpec);
        }

        // TODO: Header grouping - need to investigate API for grouping headers
        // For MVP, we'll skip header grouping serialization

        return spec;
    }

    public static ViewSchedule CreateSchedule(Document doc, ScheduleSpec spec) {
        // Find category by name
        var categoryId = FindCategoryByName(doc, spec.CategoryName);
        if (categoryId == ElementId.InvalidElementId)
            throw new ArgumentException($"Category '{spec.CategoryName}' not found in document");

        // Create schedule
        var schedule = ViewSchedule.CreateSchedule(doc, categoryId);
        schedule.Name = GetUniqueScheduleName(doc, spec.Name);

        // Apply schedule-level settings
        schedule.Definition.IsItemized = spec.IsItemized;

        // Apply fields
        ApplyFieldsToSchedule(schedule, spec);

        // Apply sort/group
        ApplySortGroupToSchedule(schedule, spec);

        return schedule;
    }

    private static string GetUniqueScheduleName(Document doc, string baseName) {
        var existingNames = new FilteredElementCollector(doc)
            .OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Select(s => s.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingNames.Contains(baseName)) return baseName;

        // Find unique name with suffix
        for (var i = 2; i < 1000; i++) {
            var candidateName = $"{baseName} ({i})";
            if (!existingNames.Contains(candidateName)) return candidateName;
        }

        return $"{baseName} ({DateTime.Now:yyyyMMdd-HHmmss})";
    }

    private static void ApplyFieldsToSchedule(ViewSchedule schedule, ScheduleSpec spec) {
        var def = schedule.Definition;
        def.ClearFields();

        // Add non-calculated fields only - the Revit API does not support creating calculated fields
        // (Formula/Percentage fields must be recreated manually in Revit)
        foreach (var fieldSpec in spec.Fields.Where(f => f.CalculatedType is null)) {
            var schedulableField = FindSchedulableField(def, schedule.Document, fieldSpec.ParameterName);
            if (schedulableField is null) {
                Debug.WriteLine($"Warning: Parameter '{fieldSpec.ParameterName}' not found for schedule '{spec.Name}'");
                continue;
            }

            var field = def.AddField(schedulableField);
            ApplyFieldProperties(field, fieldSpec);
        }

        // Log skipped calculated fields
        var calculatedFields = spec.Fields.Where(f => f.CalculatedType is not null).ToList();
        if (calculatedFields.Count > 0) {
            Debug.WriteLine(
                $"Note: {calculatedFields.Count} calculated field(s) skipped (must be recreated manually): " +
                string.Join(", ", calculatedFields.Select(f => f.ParameterName)));
        }
    }

    private static void ApplyFieldProperties(ScheduleField field, ScheduleFieldSpec fieldSpec) {
        if (!string.IsNullOrEmpty(fieldSpec.ColumnHeaderOverride)) field.ColumnHeading = fieldSpec.ColumnHeaderOverride;

        field.IsHidden = fieldSpec.IsHidden;

        // Apply column width if specified
        if (fieldSpec.ColumnWidth.HasValue && fieldSpec.ColumnWidth.Value > 0)
            field.SheetColumnWidth = fieldSpec.ColumnWidth.Value;

        // Apply display type if field supports it (cast to int for comparison since enum member names vary)
        var targetDisplayType = (ScheduleFieldDisplayType)(int)fieldSpec.DisplayType;
        if (fieldSpec.DisplayType != FieldDisplayType.Standard) {
            var canApply = fieldSpec.DisplayType switch {
                FieldDisplayType.Totals => field.CanTotal(),
                FieldDisplayType.Maximum or FieldDisplayType.Minimum or FieldDisplayType.MinAndMax =>
                    field.CanDisplayMinMax(),
                _ => false
            };

            if (canApply)
                field.DisplayType = targetDisplayType;
            else {
                Debug.WriteLine(
                    $"Warning: DisplayType '{fieldSpec.DisplayType}' not supported for field '{fieldSpec.ParameterName}'");
            }
        }
    }

    private static void ApplySortGroupToSchedule(ViewSchedule schedule, ScheduleSpec spec) {
        var def = schedule.Definition;
        def.ClearSortGroupFields();

        foreach (var sortGroupSpec in spec.SortGroup) {
            // Find the field by name
            ScheduleFieldId fieldId = null;
            for (var i = 0; i < def.GetFieldCount(); i++) {
                var field = def.GetField(i);
                if (field.GetName() == sortGroupSpec.FieldName) {
                    fieldId = field.FieldId;
                    break;
                }
            }

            if (fieldId == null) {
                Debug.WriteLine(
                    $"Warning: Field '{sortGroupSpec.FieldName}' not found for sort/group in schedule '{spec.Name}'");
                continue;
            }

            var sortOrder = sortGroupSpec.SortOrder == ScheduleSortOrder.Ascending
                ? Autodesk.Revit.DB.ScheduleSortOrder.Ascending
                : Autodesk.Revit.DB.ScheduleSortOrder.Descending;

            var sortGroupField = new ScheduleSortGroupField(fieldId, sortOrder) {
                ShowHeader = sortGroupSpec.ShowHeader,
                ShowFooter = sortGroupSpec.ShowFooter,
                ShowBlankLine = sortGroupSpec.ShowBlankLine
            };

            def.AddSortGroupField(sortGroupField);
        }
    }

    private static ElementId FindCategoryByName(Document doc, string categoryName) {
        var categories = doc.Settings.Categories;
        foreach (Category cat in categories) {
            if (cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                return cat.Id;
        }

        return ElementId.InvalidElementId;
    }

    private static SchedulableField FindSchedulableField(ScheduleDefinition def, Document doc, string parameterName) {
        var schedulableFields = def.GetSchedulableFields();
        foreach (var sf in schedulableFields) {
            var name = sf.GetName(doc);
            if (name.Equals(parameterName, StringComparison.OrdinalIgnoreCase)) return sf;
        }

        return null;
    }
}