namespace SyntaxCircus.Common.Tests;

// The baselines deliberately use the argument shapes CA1873 objects to.
#pragma warning disable CA1873

public class LogIfExtensionsTests
{
    private sealed record Entry(
        LogLevel Level,
        EventId EventId,
        Exception? Exception,
        string Message,
        IReadOnlyList<KeyValuePair<string, object?>> State)
    {
        public IEnumerable<(string, object?, Type?)> Fields => State.Select(p => (p.Key, p.Value, p.Value?.GetType()));
    }

    private sealed class RecordingLogger(LogLevel minimum = LogLevel.Trace) : ILogger
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new Entry(
                logLevel,
                eventId,
                exception,
                formatter(state, exception),
                [.. (IReadOnlyList<KeyValuePair<string, object?>>)state!]));
    }

    private static void ShouldMatch(Entry actual, Entry expected)
    {
        actual.Level.ShouldBe(expected.Level);
        actual.Message.ShouldBe(expected.Message);
        actual.Fields.ShouldBe(expected.Fields);
    }

    [Fact]
    public void LogIf_ProducesSameStructuredEntryAsLogInformation()
    {
        var logger = new RecordingLogger();
        var reason = DayOfWeek.Friday;
        var result = (Exception: (Exception?)new InvalidOperationException("expired"), Attempt: 3);

        logger.LogInformation(
            "Access token refused ({Reason}): {Detail} after {Attempt}",
            reason,
            result.Exception?.Message,
            result.Attempt);
        logger.LogIf(
            LogLevel.Information,
            "Access token refused ({Reason}): {Detail} after {Attempt}",
            $"{reason}{result.Exception?.Message}{result.Attempt}");

        ShouldMatch(logger.Entries[1], logger.Entries[0]);
    }

    [Fact]
    public void LogIf_FormatsAlignmentAndBraces_BehaveAsInLogInformation()
    {
        var logger = new RecordingLogger();
        var amount = 1234;
        var at = new TimeSpan(1, 2, 3);

        logger.LogInformation(@"{{x}} {Amount:#,##0} [{Amount2,6:N0}] {At:hh\:mm} {@Raw}", amount, amount, at, at);
        logger.LogIf(
            LogLevel.Information,
            @"{{x}} {Amount:#,##0} [{Amount2,6:N0}] {At:hh\:mm} {@Raw}",
            $"{amount}{amount}{at}{at}");

        ShouldMatch(logger.Entries[1], logger.Entries[0]);
    }

    [Fact]
    public void LogIf_NullValues_BehaveAsInLogInformation()
    {
        var logger = new RecordingLogger();
        string? detail = null;

        logger.LogInformation("{First} {Second}", detail, detail);
        logger.LogIf(LogLevel.Information, "{First} {Second}", $"{detail}{detail}");

        ShouldMatch(logger.Entries[1], logger.Entries[0]);
    }

    [Fact]
    public void LogIf_BareNullAndDefaultValues_AreAccepted()
    {
        var logger = new RecordingLogger();

        logger.LogInformation("{First} {Second} {Third}", 1, null, default(object));
        logger.LogIf(LogLevel.Information, "{First} {Second} {Third}", $"{1}{null}{default}");

        ShouldMatch(logger.Entries[1], logger.Entries[0]);
    }

    [Fact]
    public void LogIf_DisabledLevel_DoesNotEvaluateValuesOrLog()
    {
        var logger = new RecordingLogger(LogLevel.None);
        var evaluations = 0;
        var exception = new InvalidOperationException();

        logger.LogIf(LogLevel.Error, "{A} {B}", $"{++evaluations}{++evaluations}");
        logger.LogIf(LogLevel.Error, exception, "{A}", $"{++evaluations}");
        logger.LogIf(LogLevel.Error, new EventId(1), "{A}", $"{++evaluations}");
        logger.LogIf(LogLevel.Error, new EventId(1), exception, "{A}", $"{++evaluations}");

        evaluations.ShouldBe(0);
        logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void LogIf_EnabledLevel_EvaluatesEachValueOnceInOrder()
    {
        var logger = new RecordingLogger();
        var evaluations = 0;

        logger.LogIf(LogLevel.Information, "{A} {B}", $"{++evaluations}{++evaluations}");

        evaluations.ShouldBe(2);
        logger.Entries.ShouldHaveSingleItem().Message.ShouldBe("1 2");
    }

    [Fact]
    public void LogIf_Overloads_PassExceptionAndEventIdThrough()
    {
        var logger = new RecordingLogger();
        var exception = new InvalidOperationException("boom");
        var eventId = new EventId(42, "Refused");
        var n = 0;

        logger.LogIf(LogLevel.Error, "{N}", $"{++n}");
        logger.LogIf(LogLevel.Error, exception, "{N}", $"{++n}");
        logger.LogIf(LogLevel.Error, eventId, "{N}", $"{++n}");
        logger.LogIf(LogLevel.Error, eventId, exception, "{N}", $"{++n}");

        logger.Entries.Select(e => (e.Level, e.EventId, e.Exception, e.Message)).ShouldBe(
        [
            (LogLevel.Error, default, null, "1"),
            (LogLevel.Error, default, exception, "2"),
            (LogLevel.Error, eventId, null, "3"),
            (LogLevel.Error, eventId, exception, "4"),
        ]);
    }

    [Fact]
    public void LogIf_NullLogger_Throws()
    {
        ILogger logger = null!;

        Should.Throw<ArgumentNullException>(() => logger.LogIf(LogLevel.Information, "{A}", $"{1}"));
    }
}
