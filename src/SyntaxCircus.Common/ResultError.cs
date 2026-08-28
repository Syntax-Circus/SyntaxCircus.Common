namespace SyntaxCircus.Common;

public sealed record ResultError
{
    public ResultError(
        string code,
        string message,
        ResultErrorKind kind,
        string? target = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The result error kind is not defined.");
        }

        if (target is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(target);

            if (kind != ResultErrorKind.Validation)
            {
                throw new ArgumentException(
                    "Only validation errors may specify a target.",
                    nameof(target));
            }
        }

        Code = code;
        Message = message;
        Kind = kind;
        Target = target;
    }

    public string Code { get; }

    public string Message { get; }

    public ResultErrorKind Kind { get; }

    public string? Target { get; }
}
