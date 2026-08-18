using System.Reflection;
using LendingLibrary.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LendingLibrary.Web.Infrastructure;

/// <summary>Rejects passwords found in the bundled common/breached-password list.</summary>
public class CommonPasswordValidator : IPasswordValidator<ApplicationUser>
{
    private static readonly Lazy<HashSet<string>> CommonPasswords = new(LoadCommonPasswords);

    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (password is not null && CommonPasswords.Value.Contains(password))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "CommonPassword",
                Description = "This password is too common. Please choose a less predictable password."
            }));
        }

        return Task.FromResult(IdentityResult.Success);
    }

    private static HashSet<string> LoadCommonPasswords()
    {
        var assembly = typeof(CommonPasswordValidator).Assembly;
        using var stream = assembly.GetManifestResourceStream("LendingLibrary.Web.Infrastructure.CommonPasswords.txt")
            ?? throw new InvalidOperationException("Embedded resource 'CommonPasswords.txt' was not found.");
        using var reader = new StreamReader(stream);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                set.Add(trimmed);
            }
        }

        return set;
    }
}
