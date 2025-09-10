public readonly struct Result<T> {
    private readonly T _value;
    private readonly Exception _error;

    private Result(T value, Exception error) {
        this._value = value;
        this._error = error;
    }

    public void Deconstruct(out T value, out Exception error) {
        value = this._value;
        error = this._error;
    }

    public (T value, Exception error) AsTuple() => (this._value, this._error);

    public static implicit operator Result<T>(T value) =>
        new(value, null!);

    public static implicit operator Result<T>(Exception error) =>
        new(default!, error);
}