using LendingLibrary.Web.Domain.Enums;

namespace LendingLibrary.Web.Domain.Entities;

public class Loan
{
    public Guid Id { get; set; }

    public Guid CatalogueItemId { get; set; }

    public CatalogueItem CatalogueItem { get; set; } = null!;

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public DateTimeOffset BorrowedAtUtc { get; set; }

    public DateTimeOffset DueAtUtc { get; set; }

    public DateTimeOffset? ReturnedAtUtc { get; set; }

    public LoanStatus Status { get; set; }

    public bool IsOverdue(DateTimeOffset now) => Status == LoanStatus.Active && DueAtUtc < now;
}
