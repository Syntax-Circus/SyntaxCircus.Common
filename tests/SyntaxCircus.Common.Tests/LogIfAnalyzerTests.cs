using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SyntaxCircus.Common.Analyzers;

namespace SyntaxCircus.Common.Tests;

public class LogIfAnalyzerTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string statements)
    {
        var source = $$"""
            #nullable enable
            using System;
            using Microsoft.Extensions.Logging;
            using SyntaxCircus.Common;

            public static class Subject
            {
                public static void Run(ILogger logger, int a, int b, string text, Exception error)
                {
                    {{statements}}
                }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(LogIfExtensions).Assembly.Location)
            .Distinct()
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Subject",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // A snippet that does not compile would make "no diagnostics" meaningless.
        compilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ShouldBeEmpty();

        return await compilation
            .WithAnalyzers([new LogIfAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A} {B}", $"{a}");""", 2, 1)]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A}", $"{a}{b}");""", 1, 2)]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A}", $"");""", 1, 0)]
    [InlineData("""logger.LogIf(LogLevel.Information, "no placeholders", $"{a}");""", 0, 1)]
    [InlineData("""logger.LogIf(LogLevel.Information, "{{A}} {B}", $"{a}{b}");""", 1, 2)]
    [InlineData("""logger.LogIf(LogLevel.Information, "{Id} {Id}", $"{a}");""", 2, 1)]
    [InlineData("""logger.LogIf(LogLevel.Error, error, "{A} {B}", $"{a}");""", 2, 1)]
    [InlineData("""logger.LogIf(LogLevel.Error, new EventId(1), "{A} {B}", $"{a}");""", 2, 1)]
    [InlineData("""logger.LogIf(LogLevel.Error, new EventId(1), error, "{A} {B}", $"{a}");""", 2, 1)]
    [InlineData("""LogIfExtensions.LogIf(logger, LogLevel.Information, "{A} {B}", $"{a}");""", 2, 1)]
    [InlineData("""logger.LogIf(message: "{A} {B}", level: LogLevel.Information, values: $"{a}");""", 2, 1)]
    [InlineData("""const string Template = "{A} {B}"; logger.LogIf(LogLevel.Information, Template, $"{a}");""", 2, 1)]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A} " + "{B}", $"{a}");""", 2, 1)]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A}", $"{a}" + $"{b}");""", 1, 2)]
    public async Task Mismatch_IsReported(string statement, int placeholders, int values)
    {
        var diagnostics = await AnalyzeAsync(statement);

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("SCLOG001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBe($"Message template placeholders: {placeholders}; values supplied: {values}");
    }

    [Theory]
    [InlineData("{A}} {B}")]
    [InlineData("{A}}}")]
    [InlineData("}} {A}")]
    [InlineData("{{{A}}}")]
    [InlineData("{{{{A}}}}")]
    [InlineData("{A {B}")]
    [InlineData("{} {B}")]
    [InlineData("{A,6:N2} {@B} {C:#,##0}")]
    public async Task PlaceholderCount_AgreesWithTheLogger(string template)
    {
        var expected = RuntimePlaceholderCount(template);

        (await AnalyzeAsync(Call(template, expected))).ShouldBeEmpty();
        (await AnalyzeAsync(Call(template, expected + 1))).ShouldHaveSingleItem()
            .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBe($"Message template placeholders: {expected}; values supplied: {expected + 1}");
    }

    [Theory]
    [InlineData("{A}}")]
    [InlineData("} {A}")]
    [InlineData("{{A}")]
    [InlineData("{A}}x")]
    [InlineData("{A} {")]
    [InlineData("{A,wide}")]
    public async Task TemplateTheLoggerRejects_IsReported(string template)
    {
        Should.Throw<FormatException>(() => RuntimePlaceholderCount(template));

        var diagnostic = (await AnalyzeAsync(Call(template, 1))).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("SCLOG004");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    // With no values the logger prints the template as it is, so it never parses it.
    [Fact]
    public async Task TemplateWithoutValues_IsNotParsedForValidity()
    {
        (await AnalyzeAsync(Call("a } b", 0))).ShouldBeEmpty();
    }

    private static string Call(string template, int values)
    {
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(template, quote: true);
        return $"logger.LogIf(LogLevel.Information, {literal}, $\"{string.Concat(Enumerable.Repeat("{a}", values))}\");";
    }

    // The logger's own state holds one entry per placeholder it found, plus the original template.
    private static int RuntimePlaceholderCount(string template)
    {
        var logger = new CountingLogger();
#pragma warning disable CA1873, CA2254
        logger.LogInformation(template, new object?[] { 0 });
#pragma warning restore CA1873, CA2254
        return logger.StateCount - 1;
    }

    private sealed class CountingLogger : ILogger
    {
        public int StateCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => StateCount = ((IReadOnlyList<KeyValuePair<string, object?>>)state!).Count;
    }

    [Theory]
    [InlineData("""logger.LogIf(LogLevel.Information, $"User {text}", $"");""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "User " + text, $"");""")]
    [InlineData("""var template = $"User {text}"; logger.LogIf(LogLevel.Information, template, $"");""")]
    [InlineData("""logger.LogIf(LogLevel.Information, text, $"{a}");""")]
    [InlineData("""logger.LogIf(LogLevel.Error, error, text, $"{a}");""")]
    public async Task TemplateThatIsNotConstant_IsReported(string statement)
    {
        var diagnostic = (await AnalyzeAsync(statement)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("SCLOG002");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData("""logger.LogIf(LogLevel.Information, "Started", default);""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "Started", new());""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "Started", default(LogIfInterpolatedStringHandler));""")]
    public async Task ValuesThatAreNotAnInterpolatedString_AreReported(string statement)
    {
        var diagnostic = (await AnalyzeAsync(statement)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("SCLOG003");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A} {B}", $"{a}{b}");""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "no placeholders", $"");""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "Expected {{id}}", $"");""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "{{{A}}} {B,5:N0} {@C} {D:#,##0}", $"{a}{b}{a}{b}");""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A}", $"{$"{a} of {b}"}");""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A} {B}", $"{a}" + $"{b}");""")]
    [InlineData("""const string Name = "{A}"; logger.LogIf(LogLevel.Information, $"{Name} done", $"{a}");""")]
    [InlineData("""logger.LogIf(LogLevel.Information, "{A} {B}", ($"{a}{b}"));""")]
    [InlineData("""logger.LogInformation("{A} {B}", a, b);""")]
    public async Task MatchingOrUnknowableCalls_AreNotReported(string statement)
    {
        var diagnostics = await AnalyzeAsync(statement);

        diagnostics.ShouldBeEmpty();
    }
}
