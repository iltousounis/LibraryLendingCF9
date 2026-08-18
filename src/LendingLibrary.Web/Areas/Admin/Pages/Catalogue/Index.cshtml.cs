using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Areas.Admin.Pages.Catalogue;

public class IndexModel(ICatalogueService catalogueService) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public ItemType? Type { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<CatalogueItem> Results { get; private set; } = new([], 1, PageSize, 0);

    public PagerViewModel Pager => new()
    {
        Page = Results.Page,
        TotalPages = Results.TotalPages,
        PageUrl = p => Url.Page("./Index", new { q = Q, type = Type, page = p })!
    };

    public async Task OnGetAsync()
    {
        var query = new CatalogueSearchQuery(Q, Type);
        Results = await catalogueService.SearchAsync(query, PageNumber, PageSize);
    }
}
