using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Areas.Admin.Pages.Loans;

public class OverdueModel(ILendingService lendingService) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<Loan> Results { get; private set; } = new([], 1, PageSize, 0);

    [TempData]
    public string? StatusMessage { get; set; }

    public PagerViewModel Pager => new()
    {
        Page = Results.Page,
        TotalPages = Results.TotalPages,
        PageUrl = p => Url.Page("./Overdue", new { page = p })!
    };

    public async Task OnGetAsync()
    {
        Results = await lendingService.GetOverdueLoansAsync(PageNumber, PageSize);
    }

    public async Task<IActionResult> OnPostReturnAsync(Guid loanId)
    {
        var result = await lendingService.ReturnAsync(loanId);
        StatusMessage = result.Succeeded ? "Marked as returned." : result.Error;
        return RedirectToPage("./Overdue", new { page = PageNumber });
    }
}
