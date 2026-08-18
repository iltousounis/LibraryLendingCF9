using LendingLibrary.Web.Infrastructure;

namespace LendingLibrary.UnitTests;

public class PagedResultTests
{
    [Theory]
    [InlineData(25, 10, 3)]
    [InlineData(20, 10, 2)]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expectedTotalPages)
    {
        var result = new PagedResult<int>([], Page: 1, PageSize: pageSize, TotalCount: totalCount);

        Assert.Equal(expectedTotalPages, result.TotalPages);
    }

    [Fact]
    public void TotalPages_ZeroPageSize_DoesNotThrow()
    {
        var result = new PagedResult<int>([], Page: 1, PageSize: 0, TotalCount: 10);

        Assert.Equal(0, result.TotalPages);
    }
}
