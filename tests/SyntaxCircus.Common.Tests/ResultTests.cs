namespace SyntaxCircus.Common.Tests;

public class ResultTests
{
    [Fact]
    public void Success_HasNoErrors()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GenericSuccess_ExposesValue()
    {
        var result = Result<string>.Success("accepted");

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe("accepted");
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GenericSuccess_RejectsNullValue()
    {
        Should.Throw<ArgumentNullException>(() => Result<string>.Success(null!));
    }

    [Fact]
    public void Failure_RequiresAnError()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void Failure_ExposesErrorsAndFailureState()
    {
        var error = new ResultError(
            "widget-not-found",
            "The widget was not found.",
            ResultErrorKind.NotFound);

        var result = Result.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().ShouldBe(error);
    }

    [Fact]
    public void GenericFailure_ThrowsWhenValueIsAccessed()
    {
        var result = Result<string>.Failure(new ResultError(
            "widget-not-found",
            "The widget was not found.",
            ResultErrorKind.NotFound));

        var exception = Should.Throw<InvalidOperationException>(() => _ = result.Value);

        exception.Message.ShouldContain("failure", Case.Insensitive);
    }

    [Fact]
    public void Failure_RejectsErrorsWithDifferentKinds()
    {
        var first = new ResultError(
            "name-required",
            "A name is required.",
            ResultErrorKind.Validation,
            "name");
        var second = new ResultError(
            "widget-conflict",
            "The widget conflicts with an existing widget.",
            ResultErrorKind.Conflict);

        Should.Throw<ArgumentException>(() => Result.Failure(first, second));
    }

    [Fact]
    public void Failure_RejectsMultipleNonValidationErrors()
    {
        var first = new ResultError(
            "widget-not-found",
            "The widget was not found.",
            ResultErrorKind.NotFound);
        var second = new ResultError(
            "owner-not-found",
            "The owner was not found.",
            ResultErrorKind.NotFound);

        Should.Throw<ArgumentException>(() => Result.Failure(first, second));
    }

    [Fact]
    public void ValidationFailure_AllowsMultipleTargetedErrors()
    {
        var first = new ResultError(
            "name-required",
            "A name is required.",
            ResultErrorKind.Validation,
            "name");
        var second = new ResultError(
            "name-too-long",
            "The name is too long.",
            ResultErrorKind.Validation,
            "name");

        var result = Result.Failure(first, second);

        result.Errors.ShouldBe([first, second]);
    }

    [Fact]
    public void ResultError_RejectsTargetForNonValidationKind()
    {
        Should.Throw<ArgumentException>(() => new ResultError(
            "widget-not-found",
            "The widget was not found.",
            ResultErrorKind.NotFound,
            "widgetId"));
    }

    [Theory]
    [InlineData("", "Message")]
    [InlineData("code", "")]
    [InlineData(" ", "Message")]
    [InlineData("code", " ")]
    public void ResultError_RejectsMissingCodeOrMessage(string code, string message)
    {
        Should.Throw<ArgumentException>(() => new ResultError(
            code,
            message,
            ResultErrorKind.Failure));
    }

    [Fact]
    public void Failure_DefensivelyCopiesAdditionalErrors()
    {
        var first = new ResultError(
            "name-required",
            "A name is required.",
            ResultErrorKind.Validation,
            "name");
        var second = new ResultError(
            "description-required",
            "A description is required.",
            ResultErrorKind.Validation,
            "description");
        var replacement = new ResultError(
            "owner-required",
            "An owner is required.",
            ResultErrorKind.Validation,
            "ownerId");
        var additional = new[] { second };

        var result = Result.Failure(first, additional);
        additional[0] = replacement;

        result.Errors.ShouldBe([first, second]);
    }
}
