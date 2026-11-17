namespace CqrsApiExample.Domain.Common.Results;

/// <summary>
///     Lets generic code (e.g. the handler decorators) inspect a result and build
///     a failed one without knowing the concrete value type.
/// </summary>
public interface IFailureResult<TSelf> where TSelf : IFailureResult<TSelf>
{
    bool IsError { get; }

    IReadOnlyList<Error> Errors { get; }

    static abstract TSelf Failure(IReadOnlyList<Error> errors);
}

/// <summary>
///     Either a value or a non-empty list of errors. Expected failures are
///     returned as errors; exceptions are reserved for unexpected ones.
/// </summary>
public sealed class Result<TValue> : IFailureResult<Result<TValue>>
{
    private readonly TValue? _value;

    private Result(TValue value)
    {
        _value = value;
        Errors = [];
    }

    private Result(IReadOnlyList<Error> errors)
    {
        if (errors.Count == 0)
        {
            throw new ArgumentException("A failed result needs at least one error.", nameof(errors));
        }

        Errors = errors;
    }

    public bool IsError => Errors.Count > 0;

    public IReadOnlyList<Error> Errors { get; }

    public TValue Value => IsError
        ? throw new InvalidOperationException("A failed result has no value. Check IsError first.")
        : _value!;

    public static Result<TValue> Success(TValue value) => new(value);

    public static Result<TValue> Failure(IReadOnlyList<Error> errors) => new(errors);

    public TResult Match<TResult>(Func<TValue, TResult> onValue,
                                  Func<IReadOnlyList<Error>, TResult> onError)
    {
        return IsError ? onError(Errors) : onValue(_value!);
    }

    public static implicit operator Result<TValue>(TValue value) => new(value);

    public static implicit operator Result<TValue>(Error error) => new([error]);

    public static implicit operator Result<TValue>(List<Error> errors) => new(errors);
}
