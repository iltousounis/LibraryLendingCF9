using LendingLibrary.Web.Domain.Enums;

namespace LendingLibrary.Web.Domain.Entities;

public class CatalogueItem
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    public ItemType ItemType { get; set; }

    public string? Authors { get; set; }

    public string? Publisher { get; set; }

    public string? Isbn { get; set; }

    public int? PublicationYear { get; set; }

    public string? Description { get; set; }

    public string? CoverImageUrl { get; set; }

    public int TotalUnits { get; set; }

    public int AvailableUnits { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>Postgres xmin system column, used as an optimistic concurrency token.</summary>
    public uint RowVersion { get; set; }

    public ICollection<Loan> Loans { get; set; } = [];

    public ICollection<Reservation> Reservations { get; set; } = [];
}
