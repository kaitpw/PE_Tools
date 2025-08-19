public readonly struct Result<T>
    where T : notnull {
    private readonly T _value;
    private readonly Exception _error;
    private readonly bool _isSuccess;

    public T Value 
    { 
        get 
        {
            if (!_isSuccess)
                throw new InvalidOperationException($"Cannot access Value when Result is in error state. Error: {_error.Message}");
            return _value;
        }
    }

    public Exception Error 
    { 
        get 
        {
            if (_isSuccess)
                throw new InvalidOperationException("Cannot access Error when Result is in success state");
            return _error;
        }
    }

    private Result(T value, Exception error, bool isSuccess) {
        _value = value;
        _error = error;
        _isSuccess = isSuccess;
    }

    public void Deconstruct(out T value, out Exception error) {
        value = _value;
        error = _error;
    }

    // Success case
    public static implicit operator Result<T>(T value) =>
        new(value, null!, true);

    // Error case
    public static implicit operator Result<T>(Exception error) =>
        new(default!, error, false);
}