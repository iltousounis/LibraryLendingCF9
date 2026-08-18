using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;

namespace LendingLibrary.Web.Services.Abstractions;

public interface IReservationService
{
    /// <summary>Atomically holds a unit (same conditional-decrement pattern as borrowing) and creates a Pending reservation.</summary>
    Task<ReservationOperationResult> ReserveAsync(Guid userId, Guid catalogueItemId, CancellationToken cancellationToken = default);

    /// <summary>Ownership-checked: only the reserving user may cancel their own pending reservation.</summary>
    Task<ReservationOperationResult> CancelAsync(Guid reservationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Admin override: cancels regardless of owner (e.g. a no-show).</summary>
    Task<ReservationOperationResult> AdminCancelAsync(Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>Admin: converts a pending reservation into an active loan at pickup. The held unit is not re-decremented.</summary>
    Task<ReservationOperationResult> FulfilAsync(Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>Moves past-expiry Pending reservations to Expired and releases their held units. Returns the count expired.</summary>
    Task<int> ExpireStaleReservationsAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<Reservation>> GetUserReservationsAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>All pending reservations across all users, for admin fulfilment.</summary>
    Task<PagedResult<Reservation>> GetPendingReservationsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
