namespace SyntaxCircus.Common;

public class Result
{
    private static readonly Result SuccessResult = new(true, []);

    private protected Result(bool isSuccess, IReadOnlyList<ResultError> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public IReadOnlyList<ResultError> Errors { get; }

    public static Result Success() => SuccessResult;

    public static Result Failure(ResultError first, params ResultError[] additional) =>
        new(false, ResultErrorCollection.Create(first, additional));
}
