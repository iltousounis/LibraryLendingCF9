using System.ComponentModel.DataAnnotations;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LendingLibrary.Web.Areas.Admin.Pages.Users;

public class CreateModel(IUserAdminService userAdminService) : PageModel
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

        var result = await userAdminService.CreateUserAsync(Input.Email, Input.DisplayName, Input.Password, Input.Role);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create the user.");
            return Page();
        }

        return RedirectToPage("./Index");
    }

    public class InputModel
    {
        [Required, StringLength(100, MinimumLength = 1)]
        [Display(Name = "Display name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "User";
    }
}
