namespace SyntaxCircus.Common;

#pragma warning disable CA1000 // Factories are intentionally discoverable on Result<T>.
public class Result<T>
{
    private readonly T? _value;

    protected Result(bool isSuccess, T? value, IReadOnlyList<ResultError> errors)
    {
        IsSuccess = isSuccess;
        _value = value;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failure result does not have a value.");

    public IReadOnlyList<ResultError> Errors { get; }

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(true, value, []);
    }

    public static Result<T> Failure(ResultError first, params ResultError[] additional) =>
        new(false, default, ResultErrorCollection.Create(first, additional));
}
#pragma warning restore CA1000
