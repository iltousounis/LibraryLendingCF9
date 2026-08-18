using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using LendingLibrary.Web.Domain.Rules;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace LendingLibrary.Web.Services.Implementations;

public class ReservationService(AppDbContext db, TimeProvider timeProvider) : IReservationService
{
    public async Task<ReservationOperationResult> ReserveAsync(
        Guid userId, Guid catalogueItemId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Same atomic conditional decrement used for borrowing: a single guarded UPDATE, so two
        // concurrent reservations for the last copy can never both succeed.
        var rowsAffected = await db.CatalogueItems
            .Where(i => i.Id == catalogueItemId && i.AvailableUnits > 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.AvailableUnits, i => i.AvailableUnits - 1), cancellationToken);

        if (rowsAffected == 0)
        {
            var exists = await db.CatalogueItems.AnyAsync(i => i.Id == catalogueItemId, cancellationToken);
            return exists
                ? ReservationOperationResult.Failure(ReservationOperationOutcome.OutOfStock, "This item just went out of stock.")
                : ReservationOperationResult.Failure(ReservationOperationOutcome.NotFound, "Item not found.");
        }

        var now = timeProvider.GetUtcNow();
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            CatalogueItemId = catalogueItemId,
            UserId = userId,
            ReservedAtUtc = now,
            ExpiresAtUtc = now + LoanPolicy.ReservationHold,
            Status = ReservationStatus.Pending
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ReservationOperationResult.Success(reservation.Id);
    }

    public Task<ReservationOperationResult> CancelAsync(
        Guid reservationId, Guid userId, CancellationToken cancellationToken = default) =>
        CancelInternalAsync(reservationId, ownerUserId: userId, cancellationToken);

    public Task<ReservationOperationResult> AdminCancelAsync(
        Guid reservationId, CancellationToken cancellationToken = default) =>
        CancelInternalAsync(reservationId, ownerUserId: null, cancellationToken);

    private async Task<ReservationOperationResult> CancelInternalAsync(
        Guid reservationId, Guid? ownerUserId, CancellationToken cancellationToken)
    {
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        if (reservation is null)
        {
            return ReservationOperationResult.Failure(ReservationOperationOutcome.NotFound, "Reservation not found.");
        }

        if (ownerUserId is { } uid && reservation.UserId != uid)
        {
            return ReservationOperationResult.Failure(ReservationOperationOutcome.NotOwner, "You can only cancel your own reservations.");
        }

        if (reservation.Status != ReservationStatus.Pending)
        {
            return ReservationOperationResult.Failure(ReservationOperationOutcome.NotPending, "This reservation is no longer pending.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        reservation.Status = ReservationStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);

        await db.CatalogueItems
            .Where(i => i.Id == reservation.CatalogueItemId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.AvailableUnits, i => i.AvailableUnits + 1), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ReservationOperationResult.Success(reservation.Id);
    }

    public async Task<ReservationOperationResult> FulfilAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
        if (reservation is null)
        {
            return ReservationOperationResult.Failure(ReservationOperationOutcome.NotFound, "Reservation not found.");
        }

        if (reservation.Status != ReservationStatus.Pending)
        {
            return ReservationOperationResult.Failure(ReservationOperationOutcome.NotPending, "This reservation is no longer pending.");
        }

        var activeLoanCount = await db.Loans
            .CountAsync(l => l.UserId == reservation.UserId && l.Status == LoanStatus.Active, cancellationToken);
        if (activeLoanCount >= LoanPolicy.MaxActiveLoansPerUser)
        {
            return ReservationOperationResult.Failure(
                ReservationOperationOutcome.LoanLimitReached,
                $"This user already has {LoanPolicy.MaxActiveLoansPerUser} active loans. They must return one before this reservation can be fulfilled.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        reservation.Status = ReservationStatus.Fulfilled;

        // The unit was already decremented when the reservation was made — fulfilment just
        // transfers that hold into an active loan, not a fresh decrement.
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            CatalogueItemId = reservation.CatalogueItemId,
            UserId = reservation.UserId,
            BorrowedAtUtc = now,
            DueAtUtc = now + LoanPolicy.LoanPeriod,
            Status = LoanStatus.Active
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return ReservationOperationResult.Success(reservation.Id, loan.Id);
    }

    public async Task<int> ExpireStaleReservationsAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var stale = await db.Reservations
            .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAtUtc < now)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return 0;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        foreach (var reservation in stale)
        {
            reservation.Status = ReservationStatus.Expired;
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var group in stale.GroupBy(r => r.CatalogueItemId))
        {
            var releaseCount = group.Count();
            await db.CatalogueItems
                .Where(i => i.Id == group.Key)
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.AvailableUnits, i => i.AvailableUnits + releaseCount), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return stale.Count;
    }

    public async Task<PagedResult<Reservation>> GetUserReservationsAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Reservations.AsNoTracking().IgnoreQueryFilters()
            .Include(r => r.CatalogueItem)
            .Where(r => r.UserId == userId && r.Status == ReservationStatus.Pending)
            .OrderBy(r => r.ExpiresAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Reservation>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<Reservation>> GetPendingReservationsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Reservations.AsNoTracking().IgnoreQueryFilters()
            .Include(r => r.CatalogueItem)
            .Include(r => r.User)
            .Where(r => r.Status == ReservationStatus.Pending)
            .OrderBy(r => r.ExpiresAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<Reservation>(items, page, pageSize, totalCount);
    }
}
