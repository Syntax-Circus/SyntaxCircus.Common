using System.Runtime.CompilerServices;

namespace SyntaxCircus.Common;

/// <summary>
/// One-line logging whose arguments are only evaluated when the level is enabled.
/// </summary>
/// <remarks>
/// <para>
/// The message is an ordinary logging template, exactly as for <c>LogInformation</c>. The values for its
/// placeholders follow as holes in an interpolated string, in order and with nothing between them:
/// <c>logger.LogIf(LogLevel.Information, "{Reason}: {Detail}", $"{reason}{detail}")</c>.
/// </para>
/// <para>
/// Anything in a hole, side effects included, is skipped when the level is disabled. The package's
/// analyzer (SCLOG001 to SCLOG004) warns about mismatched values and misused arguments.
/// </para>
/// </remarks>
public static class LogIfExtensions
{
    /// <summary>Logs the message if <paramref name="level"/> is enabled.</summary>
    public static void LogIf(
        this ILogger logger,
        LogLevel level,
        string message,
        [InterpolatedStringHandlerArgument(nameof(logger), nameof(level))] LogIfInterpolatedStringHandler values)
        => Write(logger, level, default, null, message, ref values);

    /// <summary>Logs the message and exception if <paramref name="level"/> is enabled.</summary>
    public static void LogIf(
        this ILogger logger,
        LogLevel level,
        Exception? exception,
        string message,
        [InterpolatedStringHandlerArgument(nameof(logger), nameof(level))] LogIfInterpolatedStringHandler values)
        => Write(logger, level, default, exception, message, ref values);

    /// <summary>Logs the message with an event ID if <paramref name="level"/> is enabled.</summary>
    public static void LogIf(
        this ILogger logger,
        LogLevel level,
        EventId eventId,
        string message,
        [InterpolatedStringHandlerArgument(nameof(logger), nameof(level))] LogIfInterpolatedStringHandler values)
        => Write(logger, level, eventId, null, message, ref values);

    /// <summary>Logs the message with an event ID and exception if <paramref name="level"/> is enabled.</summary>
    public static void LogIf(
        this ILogger logger,
        LogLevel level,
        EventId eventId,
        Exception? exception,
        string message,
        [InterpolatedStringHandlerArgument(nameof(logger), nameof(level))] LogIfInterpolatedStringHandler values)
        => Write(logger, level, eventId, exception, message, ref values);

    private static void Write(
        ILogger logger,
        LogLevel level,
        EventId eventId,
        Exception? exception,
        string message,
        ref LogIfInterpolatedStringHandler values)
    {
        if (values.Values is not { } arguments)
        {
            return;
        }

        // The template is the caller's own; the analyzers cannot see that from here.
#pragma warning disable CA2254, CA1873
        logger.Log(level, eventId, exception, message, arguments);
#pragma warning restore CA2254, CA1873
    }
}
