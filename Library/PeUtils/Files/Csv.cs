using PeRevit.Ui;

namespace PeUtils.Files;

public class Csv<T> where T : class, new() {
    public readonly string FilePath;

    public Csv(string filePath) {
        FileUtils.ValidateFileNameAndExtension(filePath, "csv");
        this.FilePath = filePath;
    }

    // Generic CSV methods for type safety
    /// <summary>
    ///     Reads CSV data from the default state file with type safety
    /// </summary>
    public Dictionary<string, T> Read() {
        try {
            if (!File.Exists(this.FilePath)) return new Dictionary<string, T>();

            var lines = File.ReadAllLines(this.FilePath);
            if (lines.Length < 2) return new Dictionary<string, T>();

            var headers = lines[0].Split(',');
            var state = new Dictionary<string, T>();

            for (var i = 1; i < lines.Length; i++) {
                var values = lines[i].Split(',');
                if (values.Length == 0 || string.IsNullOrEmpty(values[0])) continue;

                var key = values[0];
                var row = new T();

                // Use reflection to set properties based on CSV headers
                for (var j = 1; j < headers.Length && j < values.Length; j++) {
                    var header = headers[j];
                    var value = values[j];

                    var property = typeof(T).GetProperty(header);
                    if (property != null && property.CanWrite) {
                        // Try to convert the string value to the property type
                        var convertedValue = ConvertValue(value, property.PropertyType);
                        if (convertedValue != null) property.SetValue(row, convertedValue);
                    }
                }

                state[key] = row;
            }

            return state;
        } catch {
            // TODO: Maybe return Result type instead
            new Balloon().Add(null, Log.ERR, $"Failed to read from CSV file: {this.FilePath}").Show();
            return new Dictionary<string, T>();
        }
    }

    /// <summary>
    ///     Writes CSV data to the default state file with type safety
    /// </summary>
    public void Write(Dictionary<string, T> data) {
        try {
            if (data.Count == 0) {
                File.WriteAllText(this.FilePath, string.Empty);
                return;
            }

            // Get all properties from the type
            var properties = typeof(T).GetProperties()
                .Where(p => p.CanRead)
                .ToList();

            var lines = new List<string>();

            // Add header
            var headers = new List<string> { "Key" };
            headers.AddRange(properties.Select(p => p.Name));
            lines.Add(string.Join(",", headers));

            // Add data rows
            foreach (var kvp in data) {
                var values = new List<string> { kvp.Key };
                foreach (var property in properties) {
                    var value = property.GetValue(kvp.Value);
                    values.Add(value?.ToString() ?? string.Empty);
                }

                lines.Add(string.Join(",", values));
            }

            File.WriteAllLines(this.FilePath, lines);
        } catch {
            // TODO: Maybe return Result type instead
            new Balloon().Add(Log.ERR, $"Failed to write to CSV file: {this.FilePath}").Show();
        }
    }

    /// <summary>
    ///     Gets a specific row from the CSV state file with type safety
    /// </summary>
    public T? ReadRow(string key) => this.Read().GetValueOrDefault(key);

    /// <summary>
    ///     Updates a specific row in the CSV state file with type safety
    /// </summary>
    public void WriteRow(string key, T rowData) {
        var state = this.Read();
        state[key] = rowData;
        this.Write(state);
    }

    /// <summary>
    ///     Converts a string value to the target type for CSV parsing
    /// </summary>
    private static object? ConvertValue(string value, Type targetType) {
        if (string.IsNullOrEmpty(value)) return null;

        try {
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(long)) return long.Parse(value);
            if (targetType == typeof(double)) return double.Parse(value);
            if (targetType == typeof(decimal)) return decimal.Parse(value);
            if (targetType == typeof(bool)) return bool.Parse(value);
            if (targetType == typeof(DateTime)) return DateTime.Parse(value);
            if (targetType == typeof(Guid)) return Guid.Parse(value);

            // For enums, try to parse
            if (targetType.IsEnum) return Enum.Parse(targetType, value);

            // For nullable types, handle them
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                var underlyingType = Nullable.GetUnderlyingType(targetType);
                if (underlyingType != null) return ConvertValue(value, underlyingType);
            }

            return null;
        } catch {
            return null;
        }
    }
}