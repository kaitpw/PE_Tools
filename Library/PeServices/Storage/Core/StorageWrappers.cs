namespace PeServices.Storage.Core;

// Restrictive interfaces for different operation types

public class JsonReader<T>(Json<T> json) where T : class, new() {
    public string FilePath => json.FilePath;
    public T Read() => json.Read();
}

public class JsonWriter<T>(Json<T> json, bool skipValidation = false, bool skipSchemaSave = false)
    where T : class, new() {
    public string FilePath => json.FilePath;
    public void Write(T data) => json.Write(data, skipValidation, skipSchemaSave);
}

public class JsonReadWriter<T>(Json<T> json, bool skipValidation = false, bool skipSchemaSave = false)
    where T : class, new() {
    public string FilePath => json.FilePath;
    public T Read() => json.Read();
    public void Write(T data) => json.Write(data, skipValidation, skipSchemaSave);

    public bool IsCacheValid(int maxAgeMinutes, Func<T, bool> contentValidator = null) =>
        json.IsCacheValid(maxAgeMinutes, contentValidator);
}

public class CsvReader<T>(Csv<T> csv) where T : class, new() {
    public string FilePath => csv.FilePath;
    public Dictionary<string, T> Read() => csv.Read();
}

public class CsvWriter<T>(Csv<T> csv) where T : class, new() {
    public string FilePath => csv.FilePath;
    public void Write(Dictionary<string, T> data) => csv.Write(data);
}

public class CsvReadWriter<T>(Csv<T> csv) where T : class, new() {
    public string FilePath => csv.FilePath;
    public Dictionary<string, T> Read() => csv.Read();
    public void Write(Dictionary<string, T> data) => csv.Write(data);
    public T ReadRow(string key) => csv.ReadRow(key);
    public void WriteRow(string key, T rowData) => csv.WriteRow(key, rowData);
}