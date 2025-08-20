public readonly struct Result<T> {
    private readonly T _value;
    private readonly Exception _error;

    private Result(T value, Exception error) {
        _value = value;
        _error = error;
    }

    public void Deconstruct(out T value, out Exception error) {
        value = _value;
        error = _error;
    }

    public static implicit operator Result<T>(T value) =>
        new(value, null!);

    public static implicit operator Result<T>(Exception error) =>
        new(default!, error);
}