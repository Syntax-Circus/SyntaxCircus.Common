namespace SyntaxCircus.Common;

using System.Net;

public sealed class ApiResult : Result
{
    private ApiResult(bool isSuccess, IReadOnlyList<ResultError> errors, HttpStatusCode? statusCode)
        : base(isSuccess, errors)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }

    public static new ApiResult Success() => new(true, [], null);

    public static ApiResult Failure(HttpStatusCode statusCode, string code, string message)
    {
        if ((int)statusCode is < 400 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode), statusCode, "The status code must be in the 400-599 range.");
        }

        return new(
            false,
            ResultErrorCollection.Create(new ResultError(code, message, ResultErrorKind.Passthrough), []),
            statusCode);
    }
}
