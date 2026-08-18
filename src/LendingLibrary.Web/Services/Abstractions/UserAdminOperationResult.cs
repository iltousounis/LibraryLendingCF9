namespace LendingLibrary.Web.Services.Abstractions;

public enum UserAdminOperationOutcome
{
    Success,
    NotFound,
    ValidationFailed
}

public class UserAdminOperationResult
{
    public required UserAdminOperationOutcome Outcome { get; init; }

    public string? Error { get; init; }

    public Guid? UserId { get; init; }

    public bool Succeeded => Outcome == UserAdminOperationOutcome.Success;

    public static UserAdminOperationResult Success(Guid userId) =>
        new() { Outcome = UserAdminOperationOutcome.Success, UserId = userId };

    public static UserAdminOperationResult Failure(UserAdminOperationOutcome outcome, string error) =>
        new() { Outcome = outcome, Error = error };
}
