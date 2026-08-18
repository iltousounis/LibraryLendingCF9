namespace LendingLibrary.Web.Domain.Rules;

/// <summary>
/// Single source of truth for lending constants. The borrow/return/reserve
/// business rules that consume these live in the application services (Phase 3+).
/// </summary>
public static class LoanPolicy
{
    public const int MaxActiveLoansPerUser = 3;

    public static readonly TimeSpan LoanPeriod = TimeSpan.FromDays(14);

    public static readonly TimeSpan ReservationHold = TimeSpan.FromDays(3);
}
