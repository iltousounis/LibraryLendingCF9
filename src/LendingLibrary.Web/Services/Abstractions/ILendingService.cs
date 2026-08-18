using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;

namespace LendingLibrary.Web.Services.Abstractions;

public interface ILendingService
{
    /// <summary>
    /// Atomically enforces the per-user active-loan cap and the item's stock, then creates the loan.
    /// </summary>
    Task<LendingOperationResult> BorrowAsync(Guid userId, Guid catalogueItemId, CancellationToken cancellationToken = default);

    /// <summary>Admin-performed in v1: marks the loan returned and releases the unit back to stock.</summary>
    Task<LendingOperationResult> ReturnAsync(Guid loanId, CancellationToken cancellationToken = default);

    Task<PagedResult<Loan>> GetUserLoansAsync(
        Guid userId, LoanView view, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>All active loans across all users, for admin return processing.</summary>
    Task<PagedResult<Loan>> GetActiveLoansAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>All overdue loans across all users — the admin overdue report.</summary>
    Task<PagedResult<Loan>> GetOverdueLoansAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
