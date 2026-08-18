using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;

namespace LendingLibrary.UnitTests;

public class LoanTests
{
    private static Loan MakeLoan(LoanStatus status, DateTimeOffset due) => new()
    {
        Id = Guid.NewGuid(),
        CatalogueItemId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        BorrowedAtUtc = due.AddDays(-14),
        DueAtUtc = due,
        Status = status
    };

    [Fact]
    public void IsOverdue_ActiveAndPastDue_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var loan = MakeLoan(LoanStatus.Active, now.AddDays(-1));

        Assert.True(loan.IsOverdue(now));
    }

    [Fact]
    public void IsOverdue_ActiveAndNotYetDue_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var loan = MakeLoan(LoanStatus.Active, now.AddDays(1));

        Assert.False(loan.IsOverdue(now));
    }

    [Fact]
    public void IsOverdue_Returned_ReturnsFalseEvenIfPastDue()
    {
        var now = DateTimeOffset.UtcNow;
        var loan = MakeLoan(LoanStatus.Returned, now.AddDays(-1));

        Assert.False(loan.IsOverdue(now));
    }
}
