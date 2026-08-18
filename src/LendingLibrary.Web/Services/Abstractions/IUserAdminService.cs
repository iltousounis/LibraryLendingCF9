using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;

namespace LendingLibrary.Web.Services.Abstractions;

public interface IUserAdminService
{
    Task<PagedResult<ApplicationUser>> SearchUsersAsync(
        string? searchText, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    Task<bool> IsLockedOutAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>Server-side creation, bypassing self-registration; assigns the given role.</summary>
    Task<UserAdminOperationResult> CreateUserAsync(
        string email, string displayName, string password, string role, CancellationToken cancellationToken = default);

    /// <summary>Sets or clears an indefinite lockout — the simple "enable/disable" toggle.</summary>
    Task<UserAdminOperationResult> SetLockedOutAsync(Guid userId, bool locked, CancellationToken cancellationToken = default);
}
