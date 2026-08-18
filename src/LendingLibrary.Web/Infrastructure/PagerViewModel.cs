namespace LendingLibrary.Web.Infrastructure;

/// <summary>URL building is delegated to the caller so the pager stays agnostic of each page's filters.</summary>
public class PagerViewModel
{
    public required int Page { get; init; }

    public required int TotalPages { get; init; }

    public required Func<int, string> PageUrl { get; init; }
}
