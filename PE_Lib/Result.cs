public readonly struct Result<T>
    where T : notnull {
    public T? Value { get; }
    public Exception? Error { get; }

    public bool IsSuccess => this.Error == null;

    private Result(T? value, Exception? error) {
        this.Value = value;
        this.Error = error;
    }

    public void Deconstruct(out T? value, out Exception? error) {
        value = this.Value;
        error = this.Error;
    }

    public static implicit operator Result<T>(T value) =>
        new(value, null);

    public static implicit operator Result<T>(Exception error) =>
        new(default, error);

    /// <summary>
    ///     Matches on success or failure, returning a value of type R.
    /// </summary>
    public R Match<R>(
        Func<T, R> success,
        Func<Exception, R> failure
    ) =>
        this.IsSuccess
            ? success(this.Value!)
            : failure(this.Error!); // '!' to assert non-null (safe due to IsSuccess check)

    /// <summary>
    ///     Matches on success or failure, performing an action (no return value).
    /// </summary>
    public void Match(
        Action<T> success,
        Action<Exception> failure
    ) {
        if (this.IsSuccess)
            success(this.Value!);
        else
            failure(this.Error!);
    }
}