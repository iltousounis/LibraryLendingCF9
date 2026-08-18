using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LendingLibrary.Web.Services.Implementations;

public class UserAdminService(UserManager<ApplicationUser> userManager, TimeProvider timeProvider) : IUserAdminService
{
    public async Task<PagedResult<ApplicationUser>> SearchUsersAsync(
        string? searchText, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = userManager.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = $"%{searchText.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Email!, term) || EF.Functions.ILike(u.DisplayName, term));
        }

        query = query.OrderBy(u => u.DisplayName);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<ApplicationUser>(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
        (await userManager.GetRolesAsync(user)).ToList();

    public async Task<bool> IsLockedOutAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
        await userManager.IsLockedOutAsync(user);

    public async Task<UserAdminOperationResult> CreateUserAsync(
        string email, string displayName, string password, string role, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            DisplayName = displayName.Trim(),
            EmailConfirmed = true,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return UserAdminOperationResult.Failure(
                UserAdminOperationOutcome.ValidationFailed,
                string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, role);

        return UserAdminOperationResult.Success(user.Id);
    }

    public async Task<UserAdminOperationResult> SetLockedOutAsync(
        Guid userId, bool locked, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return UserAdminOperationResult.Failure(UserAdminOperationOutcome.NotFound, "User not found.");
        }

        if (!await userManager.GetLockoutEnabledAsync(user))
        {
            await userManager.SetLockoutEnabledAsync(user, true);
        }

        await userManager.SetLockoutEndDateAsync(user, locked ? DateTimeOffset.MaxValue : null);

        return UserAdminOperationResult.Success(user.Id);
    }
}
