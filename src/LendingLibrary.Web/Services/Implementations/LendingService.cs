using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Domain.Rules;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace LendingLibrary.Web.Services.Implementations;

public class LendingService(AppDbContext db, TimeProvider timeProvider) : ILendingService
{
    public async Task<LendingOperationResult> BorrowAsync(
        Guid userId, Guid catalogueItemId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var activeLoanCount = await db.Loans
            .CountAsync(l => l.UserId == userId && l.Status == LoanStatus.Active, cancellationToken);
        if (activeLoanCount >= LoanPolicy.MaxActiveLoansPerUser)
        {
            return LendingOperationResult.Failure(
                LendingOperationOutcome.LoanLimitReached,
                $"You already have {LoanPolicy.MaxActiveLoansPerUser} active loans. Return one before borrowing another.");
        }

        // Atomic conditional decrement: a single UPDATE guarded by AvailableUnits > 0, so two
        // concurrent borrows of the last copy can never both succeed (no read-then-write race).
        var rowsAffected = await db.CatalogueItems
            .Where(i => i.Id == catalogueItemId && i.AvailableUnits > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.AvailableUnits, i => i.AvailableUnits - 1), cancellationToken);

        if (rowsAffected == 0)
        {
            var exists = await db.CatalogueItems.AnyAsync(i => i.Id == catalogueItemId, cancellationToken);
            return exists
                ? LendingOperationResult.Failure(LendingOperationOutcome.OutOfStock, "This item just went out of stock.")
                : LendingOperationResult.Failure(LendingOperationOutcome.NotFound, "Item not found.");
        }

        var now = timeProvider.GetUtcNow();
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            CatalogueItemId = catalogueItemId,
            UserId = userId,
            BorrowedAtUtc = now,
            DueAtUtc = now + LoanPolicy.LoanPeriod,
            Status = LoanStatus.Active
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return LendingOperationResult.Success(loan.Id);
    }

    public async Task<LendingOperationResult> ReturnAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        var loan = await db.Loans.SingleOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return LendingOperationResult.Failure(LendingOperationOutcome.NotFound, "Loan not found.");
        }

        if (loan.Status != LoanStatus.Active)
        {
            return LendingOperationResult.Failure(LendingOperationOutcome.AlreadyReturned, "This loan has already been returned.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        loan.Status = LoanStatus.Returned;
        loan.ReturnedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        await db.CatalogueItems
            .Where(i => i.Id == loan.CatalogueItemId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.AvailableUnits, i => i.AvailableUnits + 1), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return LendingOperationResult.Success(loan.Id);
    }

    public async Task<PagedResult<Loan>> GetUserLoansAsync(
        Guid userId, LoanView view, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var now = timeProvider.GetUtcNow();

        // IgnoreQueryFilters: a loan must remain visible in history even if its catalogue item
        // was later soft-deleted (the item's own filter would otherwise inner-join it away).
        var query = db.Loans.AsNoTracking().IgnoreQueryFilters()
            .Include(l => l.CatalogueItem)
            .Where(l => l.UserId == userId);

        query = view switch
        {
            LoanView.Current => query.Where(l => l.Status == LoanStatus.Active),
            LoanView.History => query.Where(l => l.Status == LoanStatus.Returned),
            LoanView.Overdue => query.Where(l => l.Status == LoanStatus.Active && l.DueAtUtc < now),
            _ => query
        };

        query = view == LoanView.History
            ? query.OrderByDescending(l => l.ReturnedAtUtc)
            : query.OrderBy(l => l.DueAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Loan>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<Loan>> GetActiveLoansAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Loans.AsNoTracking().IgnoreQueryFilters()
            .Include(l => l.CatalogueItem)
            .Include(l => l.User)
            .Where(l => l.Status == LoanStatus.Active)
            .OrderBy(l => l.DueAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Loan>(items, page, pageSize, totalCount);
    }
}
