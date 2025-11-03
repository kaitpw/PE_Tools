namespace PeServices.Storage.Core;

// Restrictive interfaces for different operation types

public interface ICsvIO {
    string FilePath { get; }
}

public interface JsonReader<T> {
    public string FilePath { get; }
    public T Read();
}

public interface JsonWriter<T> {
    public string FilePath { get; }
    public void Write(T data);
}

public interface JsonReadWriter<T> : JsonReader<T>, JsonWriter<T> where T : class, new() {
    public bool IsCacheValid(int maxAgeMinutes, Func<T, bool> contentValidator = null);
}

public interface CsvReader<T> {
    string FilePath { get; }
    Dictionary<string, T> Read();
    T ReadRow(string key);
}

public interface CsvWriter<T> {
    string FilePath { get; }
    void Write(Dictionary<string, T> data);
    void WriteRow(string key, T rowData);
}

public interface CsvReadWriter<T> : CsvReader<T>, CsvWriter<T> where T : class, new() {
}