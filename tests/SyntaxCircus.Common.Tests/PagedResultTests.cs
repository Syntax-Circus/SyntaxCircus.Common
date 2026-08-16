namespace SyntaxCircus.Common.Tests;

public class PagedResultTests
{
    [Theory]
    [InlineData(1, 10, 25, 3)]
    [InlineData(1, 10, 20, 2)]
    [InlineData(1, 10, 21, 3)]
    [InlineData(1, 10, 0, 0)]
    [InlineData(1, 0, 10, 0)]
    public void TotalPages_ComputesCeilingDivision(int page, int pageSize, int totalCount, int expectedTotalPages)
    {
        var result = new PagedResult<int>([], page, pageSize, totalCount);

        result.TotalPages.ShouldBe(expectedTotalPages);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void HasPreviousPage_BoundaryAtPageOne(int page, bool expected)
    {
        var result = new PagedResult<int>([], page, 10, 100);

        result.HasPreviousPage.ShouldBe(expected);
    }

    [Fact]
    public void HasNextPage_OnLastPage_IsFalse()
    {
        var result = new PagedResult<int>([], 3, 10, 25);

        result.TotalPages.ShouldBe(3);
        result.HasNextPage.ShouldBeFalse();
    }

    [Fact]
    public void HasNextPage_BeforeLastPage_IsTrue()
    {
        var result = new PagedResult<int>([], 2, 10, 25);

        result.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void Items_RoundTrips()
    {
        var items = new[] { 1, 2, 3 };

        var result = new PagedResult<int>(items, 1, 10, 3);

        result.Items.ShouldBe(items);
    }
}
