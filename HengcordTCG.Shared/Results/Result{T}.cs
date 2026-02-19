namespace HengcordTCG.Shared.Results;

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    protected internal Result(T value) : base(true, Error.None)
    {
        _value = value;
    }

    protected internal Result(Error error) : base(false, error)
    {
        _value = default;
    }

    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(Error error) => new(error);
    public new static Result<T> Failure(string code, string message) => Failure(new Error(code, message));

    public static implicit operator Result<T>(T value) => Success(value);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(Value) : onFailure(Error);
    }
}
