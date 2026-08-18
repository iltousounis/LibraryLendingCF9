using LendingLibrary.Web.Domain.Rules;

namespace LendingLibrary.UnitTests;

public class LoanPolicyTests
{
    [Fact]
    public void MaxActiveLoansPerUser_IsThree()
    {
        Assert.Equal(3, LoanPolicy.MaxActiveLoansPerUser);
    }

    [Fact]
    public void LoanPeriod_IsFourteenDays()
    {
        Assert.Equal(TimeSpan.FromDays(14), LoanPolicy.LoanPeriod);
    }

    [Fact]
    public void ReservationHold_IsThreeDays()
    {
        Assert.Equal(TimeSpan.FromDays(3), LoanPolicy.ReservationHold);
    }
}
