using Microsoft.AspNetCore.Identity;

namespace LendingLibrary.Web.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ICollection<Loan> Loans { get; set; } = [];

    public ICollection<Reservation> Reservations { get; set; } = [];
}
