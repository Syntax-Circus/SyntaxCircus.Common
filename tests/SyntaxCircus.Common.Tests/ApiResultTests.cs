namespace SyntaxCircus.Common.Tests;

public class ApiResultTests
{
    [Fact]
    public void Success_HasNoStatusCode()
    {
        var result = ApiResult.Success();

        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBeNull();
    }

    [Fact]
    public void Failure_ExposesStatusCodeAndPassthroughError()
    {
        var result = ApiResult.Failure(
            HttpStatusCode.TooManyRequests,
            "revenuecat-rate-limited",
            "RevenueCat is rate-limiting requests.");

        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("revenuecat-rate-limited");
        error.Message.ShouldBe("RevenueCat is rate-limiting requests.");
        error.Kind.ShouldBe(ResultErrorKind.Passthrough);
    }

    [Fact]
    public void ApiResult_IsAssignableToResult()
    {
        ApiResult apiResult = ApiResult.Success();
        Result baseResult = apiResult;

        baseResult.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void GenericSuccess_HasNoStatusCodeAndExposesValue()
    {
        var result = ApiResult<string>.Success("accepted");

        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBeNull();
        result.Value.ShouldBe("accepted");
    }

    [Fact]
    public void GenericSuccess_RejectsNullValue()
    {
        Should.Throw<ArgumentNullException>(() => ApiResult<string>.Success(null!));
    }

    [Fact]
    public void GenericFailure_ExposesStatusCodeAndPassthroughError()
    {
        var result = ApiResult<string>.Failure(
            HttpStatusCode.BadGateway,
            "stripe-sync-failed",
            "Stripe rejected the sync request.");

        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var error = result.Errors.ShouldHaveSingleItem();
        error.Code.ShouldBe("stripe-sync-failed");
        error.Message.ShouldBe("Stripe rejected the sync request.");
        error.Kind.ShouldBe(ResultErrorKind.Passthrough);
    }

    [Fact]
    public void GenericFailure_ThrowsWhenValueIsAccessed()
    {
        var result = ApiResult<string>.Failure(
            HttpStatusCode.ServiceUnavailable,
            "storage-timeout",
            "The storage backend timed out.");

        var exception = Should.Throw<InvalidOperationException>(() => _ = result.Value);

        exception.Message.ShouldContain("failure", Case.Insensitive);
    }

    [Fact]
    public void GenericApiResult_IsAssignableToGenericResult()
    {
        ApiResult<string> apiResult = ApiResult<string>.Success("accepted");
        Result<string> baseResult = apiResult;

        baseResult.Value.ShouldBe("accepted");
    }

    [Fact]
    public void Failure_RejectsStatusCodeOutsideErrorRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ApiResult.Failure(HttpStatusCode.OK, "not-an-error", "This is not an error."));
    }

    [Fact]
    public void GenericFailure_RejectsStatusCodeOutsideErrorRange()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ApiResult<string>.Failure(HttpStatusCode.OK, "not-an-error", "This is not an error."));
    }
}
