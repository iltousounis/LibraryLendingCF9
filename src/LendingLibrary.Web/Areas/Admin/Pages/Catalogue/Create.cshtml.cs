using System.ComponentModel.DataAnnotations;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Areas.Admin.Pages.Catalogue;

public class CreateModel(ICatalogueService catalogueService) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await catalogueService.CreateAsync(new CatalogueItemInput(
            Input.Title, Input.ItemType, Input.Authors, Input.Publisher, Input.Isbn,
            Input.PublicationYear, Input.Description, Input.CoverImageUrl, Input.TotalUnits));

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create the item.");
            return Page();
        }

        return RedirectToPage("./Index");
    }

    public class InputModel
    {
        [Required, StringLength(500)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Item type")]
        public ItemType ItemType { get; set; }

        [StringLength(500)]
        public string? Authors { get; set; }

        [StringLength(200)]
        public string? Publisher { get; set; }

        [StringLength(20)]
        [Display(Name = "ISBN")]
        public string? Isbn { get; set; }

        [Range(1450, 2100)]
        [Display(Name = "Publication year")]
        public int? PublicationYear { get; set; }

        public string? Description { get; set; }

        [Display(Name = "Cover image URL")]
        [Url]
        public string? CoverImageUrl { get; set; }

        [Required]
        [Range(0, 10000)]
        [Display(Name = "Total units")]
        public int TotalUnits { get; set; } = 1;
    }
}
