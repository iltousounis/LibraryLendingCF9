using LendingLibrary.Web.Domain.Enums;

namespace LendingLibrary.Web.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; }

    public Guid CatalogueItemId { get; set; }

    public CatalogueItem CatalogueItem { get; set; } = null!;

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public DateTimeOffset ReservedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public ReservationStatus Status { get; set; }
}
