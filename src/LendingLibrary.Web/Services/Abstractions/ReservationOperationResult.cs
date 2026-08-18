namespace LendingLibrary.Web.Services.Abstractions;

public enum ReservationOperationOutcome
{
    Success,
    NotFound,
    OutOfStock,
    LoanLimitReached,
    NotOwner,
    NotPending
}

public class ReservationOperationResult
{
    public required ReservationOperationOutcome Outcome { get; init; }

    public string? Error { get; init; }

    public Guid? ReservationId { get; init; }

    /// <summary>Populated only when FulfilAsync succeeds.</summary>
    public Guid? LoanId { get; init; }

    public bool Succeeded => Outcome == ReservationOperationOutcome.Success;

    public static ReservationOperationResult Success(Guid reservationId, Guid? loanId = null) =>
        new() { Outcome = ReservationOperationOutcome.Success, ReservationId = reservationId, LoanId = loanId };

    public static ReservationOperationResult Failure(ReservationOperationOutcome outcome, string error) =>
        new() { Outcome = outcome, Error = error };
}
