namespace LendingLibrary.Web.Services.Abstractions;

public enum LendingOperationOutcome
{
    Success,
    LoanLimitReached,
    OutOfStock,
    NotFound,
    AlreadyReturned
}

public class LendingOperationResult
{
    public required LendingOperationOutcome Outcome { get; init; }

    public string? Error { get; init; }

    public Guid? LoanId { get; init; }

    public bool Succeeded => Outcome == LendingOperationOutcome.Success;

    public static LendingOperationResult Success(Guid loanId) =>
        new() { Outcome = LendingOperationOutcome.Success, LoanId = loanId };

    public static LendingOperationResult Failure(LendingOperationOutcome outcome, string error) =>
        new() { Outcome = outcome, Error = error };
}
