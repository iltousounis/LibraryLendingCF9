using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Pages.Loans;

[Authorize(Policy = "RequireUser")]
public class CurrentModel(ILendingService lendingService, UserManager<ApplicationUser> userManager) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<Loan> Results { get; private set; } = new([], 1, PageSize, 0);

    public PagerViewModel Pager => new()
    {
        Page = Results.Page,
        TotalPages = Results.TotalPages,
        PageUrl = p => Url.Page("./Current", new { page = p })!
    };

    public async Task OnGetAsync()
    {
        var userId = Guid.Parse(userManager.GetUserId(User)!);
        Results = await lendingService.GetUserLoansAsync(userId, LoanView.Current, PageNumber, PageSize);
    }
}
