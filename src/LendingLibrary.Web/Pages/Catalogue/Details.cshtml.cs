using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Pages.Catalogue;

public class DetailsModel(ICatalogueService catalogueService) : PageModel
{
    public CatalogueItem Item { get; private set; } = null!;

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
}
