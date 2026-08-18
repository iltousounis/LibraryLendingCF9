using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Areas.Admin.Pages.Reservations;

public class IndexModel(IReservationService reservationService) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<Reservation> Results { get; private set; } = new([], 1, PageSize, 0);

    [TempData]
    public string? StatusMessage { get; set; }

    public PagerViewModel Pager => new()
    {
        Page = Results.Page,
        TotalPages = Results.TotalPages,
        PageUrl = p => Url.Page("./Index", new { page = p })!
    };

    public async Task OnGetAsync()
    {
        Results = await reservationService.GetPendingReservationsAsync(PageNumber, PageSize);
    }

    public async Task<IActionResult> OnPostFulfilAsync(Guid reservationId)
    {
        var result = await reservationService.FulfilAsync(reservationId);
        StatusMessage = result.Succeeded ? "Reservation fulfilled — loan created." : result.Error;
        return RedirectToPage("./Index", new { page = PageNumber });
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid reservationId)
    {
        var result = await reservationService.AdminCancelAsync(reservationId);
        StatusMessage = result.Succeeded ? "Reservation cancelled." : result.Error;
        return RedirectToPage("./Index", new { page = PageNumber });
    }
}
