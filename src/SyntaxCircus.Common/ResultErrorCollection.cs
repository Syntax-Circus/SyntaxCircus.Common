using System.Collections.ObjectModel;

namespace SyntaxCircus.Common;

internal static class ResultErrorCollection
{
    public static IReadOnlyList<ResultError> Create(
        ResultError first,
        ResultError[] additional)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(additional);

        var errors = new ResultError[additional.Length + 1];
        errors[0] = first;

        for (var index = 0; index < additional.Length; index++)
        {
            var error = additional[index]
                ?? throw new ArgumentException("Errors cannot contain null values.", nameof(additional));

            if (error.Kind != first.Kind)
            {
                throw new ArgumentException("All errors in a result must have the same kind.", nameof(additional));
            }

            errors[index + 1] = error;
        }

        if (errors.Length > 1 && first.Kind != ResultErrorKind.Validation)
        {
            throw new ArgumentException(
                "Only validation failures may contain multiple errors.",
                nameof(additional));
        }

        return new ReadOnlyCollection<ResultError>(errors);
    }
}
