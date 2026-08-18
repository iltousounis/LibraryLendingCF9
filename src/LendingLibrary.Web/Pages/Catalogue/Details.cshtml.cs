using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Pages.Catalogue;

public class DetailsModel(
    ICatalogueService catalogueService,
    ILendingService lendingService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public CatalogueItem Item { get; private set; } = null!;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public bool StatusIsError { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var item = await catalogueService.GetByIdAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        Item = item;
        return Page();
    }

    public async Task<IActionResult> OnPostBorrowAsync(Guid id)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("./Details", new { id }) });
        }

        var userId = userManager.GetUserId(User);
        var result = await lendingService.BorrowAsync(Guid.Parse(userId!), id);

        StatusIsError = !result.Succeeded;
        StatusMessage = result.Succeeded
            ? "Borrowed! It's due back in 14 days. See it under \"My loans\"."
            : result.Error;

        return RedirectToPage("./Details", new { id });
    }
}
