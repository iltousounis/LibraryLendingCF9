using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Areas.Admin.Pages.Users;

public class IndexModel(IUserAdminService userAdminService) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public PagedResult<ApplicationUser> Results { get; private set; } = new([], 1, PageSize, 0);

    public Dictionary<Guid, IReadOnlyList<string>> RolesByUserId { get; } = [];

    public Dictionary<Guid, bool> LockedByUserId { get; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public PagerViewModel Pager => new()
    {
        Page = Results.Page,
        TotalPages = Results.TotalPages,
        PageUrl = p => Url.Page("./Index", new { q = Q, page = p })!
    };

    public async Task OnGetAsync()
    {
        Results = await userAdminService.SearchUsersAsync(Q, PageNumber, PageSize);

        foreach (var user in Results.Items)
        {
            RolesByUserId[user.Id] = await userAdminService.GetRolesAsync(user);
            LockedByUserId[user.Id] = await userAdminService.IsLockedOutAsync(user);
        }
    }

    public async Task<IActionResult> OnPostToggleLockAsync(Guid userId, bool locked)
    {
        var result = await userAdminService.SetLockedOutAsync(userId, locked);
        StatusMessage = result.Succeeded
            ? (locked ? "User disabled." : "User re-enabled.")
            : result.Error;
        return RedirectToPage("./Index", new { q = Q, page = PageNumber });
    }
}
