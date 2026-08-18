namespace LendingLibrary.Web.Infrastructure;

public class LendingOptions
{
    public const string SectionName = "Lending";

    public int MaxActiveLoans { get; set; } = 3;

    public int LoanPeriodDays { get; set; } = 14;

    public int ReservationHoldDays { get; set; } = 3;
}
