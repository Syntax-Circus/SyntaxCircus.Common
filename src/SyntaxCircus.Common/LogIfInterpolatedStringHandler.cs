using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyntaxCircus.Common;

/// <summary>
/// Compiler support for <see cref="LogIfExtensions"/>; not intended to be used directly. Collects the
/// values of an interpolated string as logging arguments, without building a string.
/// </summary>
/// <remarks>
/// There is deliberately no <c>AppendLiteral</c>, and no support for formats or alignment, so that text
/// which would be ignored does not compile. All of that belongs in the message template.
/// </remarks>
[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Never)]
public struct LogIfInterpolatedStringHandler
{
    private readonly object?[]? _values;
    private int _count;

    /// <summary>Called by the compiler; the holes are skipped when <paramref name="isEnabled"/> is false.</summary>
    public LogIfInterpolatedStringHandler(
        int literalLength,
        int formattedCount,
        ILogger logger,
        LogLevel level,
        out bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(logger);

        isEnabled = logger.IsEnabled(level);
        _values = isEnabled ? new object?[formattedCount] : null;
        _count = 0;
    }

    // Null when the level is disabled.
    internal readonly object?[]? Values => _values;

    /// <summary>Called by the compiler for each hole.</summary>
    public void AppendFormatted<T>(T value) => _values![_count++] = value;

    /// <summary>Called by the compiler for a hole with no type of its own, such as a bare <c>null</c>.</summary>
    public void AppendFormatted(object? value) => _values![_count++] = value;
}
