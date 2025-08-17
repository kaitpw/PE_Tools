namespace PE_Lib;

public readonly struct Result<T> where T : notnull{
    public readonly T Value;
    public readonly Exception Error;

    private Result(T v, Exception e, bool success)
    {
        this.Value = v;
        this.Error = e;
    }
    
    public void Deconstruct(out T value, out Exception error)
    {
        value = this.Value;
        error = this.Error;
    }
    
    public static implicit operator Result<T>(T v) => new(v, null, true);
    public static implicit operator Result<T>(Exception e) => new(default(T), e, false);

    public R Match<R>(
        Func<T, R> success,
        Func<Exception, R> failure) =>
        this.Error != null ? success(this.Value) : failure(this.Error);
}