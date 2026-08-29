namespace SyntaxCircus.Common;

using System.Net;

#pragma warning disable CA1000 // Factories are intentionally discoverable on ApiResult<T>, matching Result<T>.
public sealed class ApiResult<T> : Result<T>
{
    private ApiResult(bool isSuccess, T? value, IReadOnlyList<ResultError> errors, HttpStatusCode? statusCode)
        : base(isSuccess, value, errors)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }

    public static new ApiResult<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(true, value, [], null);
    }

    public static ApiResult<T> Failure(HttpStatusCode statusCode, string code, string message)
    {
        if ((int)statusCode is < 400 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "The status code must be in the 400-599 range.");
        }

        return new(
            false,
            default,
            ResultErrorCollection.Create(new ResultError(code, message, ResultErrorKind.Passthrough), []),
            statusCode);
    }
}
#pragma warning restore CA1000
