using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SyntaxCircus.Common.Analyzers;

/// <summary>
/// Checks <c>LogIf</c> calls: the number of values must match the placeholders (SCLOG001, the
/// counterpart of CA2017), the message template must be a constant (SCLOG002), and the values must be an
/// interpolated string (SCLOG003).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LogIfAnalyzer : DiagnosticAnalyzer
{
    public const string ValueCountId = "SCLOG001";
    public const string TemplateNotConstantId = "SCLOG002";
    public const string ValuesNotInterpolatedId = "SCLOG003";
    public const string InvalidTemplateId = "SCLOG004";

    private static readonly char[] NameDelimiters = { ',', ':' };

    private static readonly DiagnosticDescriptor ValueCountRule = new(
        ValueCountId,
        "LogIf values do not match the message template",
        "Message template placeholders: {0}; values supplied: {1}",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "LogIf needs exactly one value for each placeholder in its message template, in order.");

    private static readonly DiagnosticDescriptor TemplateNotConstantRule = new(
        TemplateNotConstantId,
        "LogIf message template is not constant",
        "The message template must be a constant; pass run-time values in the last argument",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A template built at run time is built even when the level is disabled, loses its structured fields, and is read as template text.");

    private static readonly DiagnosticDescriptor ValuesNotInterpolatedRule = new(
        ValuesNotInterpolatedId,
        "LogIf values are not an interpolated string",
        "Write the values as an interpolated string in the call, using an empty one when there are none",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The compiler only collects values from an interpolated string written in the call. With anything else, such as default, nothing may ever be logged.");

    private static readonly DiagnosticDescriptor InvalidTemplateRule = new(
        InvalidTemplateId,
        "LogIf message template is invalid",
        "The logger cannot parse this message template; check for unmatched braces and bad alignment",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The logger throws FormatException for this template when the level is enabled.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(ValueCountRule, TemplateNotConstantRule, ValuesNotInterpolatedRule, InvalidTemplateRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var extensions = start.Compilation.GetTypeByMetadataName("SyntaxCircus.Common.LogIfExtensions");
            if (extensions is not null)
            {
                start.RegisterOperationAction(operation => Analyze(operation, extensions), OperationKind.Invocation);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol extensions)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != "LogIf"
            || !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, extensions))
        {
            return;
        }

        string? template = null;
        int? values = null;
        foreach (var argument in invocation.Arguments)
        {
            // Skips the logger of an extension-style call, which is not written as an argument.
            if (argument.Syntax is not ArgumentSyntax syntax)
            {
                continue;
            }

            if (argument.Parameter?.Name == "message")
            {
                if (!argument.Value.ConstantValue.HasValue)
                {
                    context.ReportDiagnostic(Diagnostic.Create(TemplateNotConstantRule, syntax.GetLocation()));
                }

                template = argument.Value.ConstantValue.Value as string;
            }
            else if (argument.Parameter?.Name == "values")
            {
                values = CountValues(syntax.Expression);
                if (values is null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ValuesNotInterpolatedRule, syntax.GetLocation()));
                }
            }
        }

        if (template is null || values is null)
        {
            return;
        }

        var placeholders = ParseTemplate(template, out var compositeFormat);

        // With no values the logger prints the template as it is, so only then can it not be invalid.
        if (values > 0 && !IsValidFormat(compositeFormat, placeholders))
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidTemplateRule, invocation.Syntax.GetLocation()));
        }
        else if (placeholders != values)
        {
            context.ReportDiagnostic(Diagnostic.Create(ValueCountRule, invocation.Syntax.GetLocation(), placeholders, values));
        }
    }

    // The holes of an interpolated string, or of several joined with '+'. Null for anything else.
    private static int? CountValues(ExpressionSyntax expression) => expression switch
    {
        InterpolatedStringExpressionSyntax interpolated => interpolated.Contents.Count(c => c is InterpolationSyntax),
        ParenthesizedExpressionSyntax parenthesized => CountValues(parenthesized.Expression),
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression)
            => CountValues(binary.Left) + CountValues(binary.Right),
        _ => null,
    };

    // Mirrors how Microsoft.Extensions.Logging parses a template: it counts the placeholders and
    // replaces each name with its position, giving a composite format string. The tests compare this
    // with the logger itself.
    private static int ParseTemplate(string template, out string compositeFormat)
    {
        var format = new StringBuilder(template.Length);
        var count = 0;
        var index = 0;
        while (index < template.Length)
        {
            var open = FindBrace(template, '{', index);
            var close = FindBrace(template, '}', open);
            if (close == template.Length)
            {
                format.Append(template, index, template.Length - index);
                break;
            }

            // Alignment and format, from the first ',' or ':', are kept.
            var nameEnd = template.IndexOfAny(NameDelimiters, open, close - open);
            if (nameEnd < 0)
            {
                nameEnd = close;
            }

            format.Append(template, index, open - index + 1);
            format.Append(count.ToString(CultureInfo.InvariantCulture));
            format.Append(template, nameEnd, close - nameEnd + 1);

            count++;
            index = close + 1;
        }

        compositeFormat = format.ToString();
        return count;
    }

    private static bool IsValidFormat(string compositeFormat, int placeholders)
    {
        try
        {
            // Null arguments: only the syntax matters here.
            _ = string.Format(CultureInfo.InvariantCulture, compositeFormat, new object[placeholders]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // The first unescaped brace from start, or the length of the template. An even run of braces is
    // escaped. In an odd run the last '{' opens a placeholder and the first '}' closes one.
    private static int FindBrace(string template, char brace, int start)
    {
        var found = template.Length;
        var run = 0;
        for (var i = start; i < template.Length; i++)
        {
            if (run > 0 && template[i] != brace)
            {
                if (run % 2 != 0)
                {
                    break;
                }

                run = 0;
                found = template.Length;
            }
            else if (template[i] == brace)
            {
                if (brace == '{' || run == 0)
                {
                    found = i;
                }

                run++;
            }
        }

        return found;
    }
}
