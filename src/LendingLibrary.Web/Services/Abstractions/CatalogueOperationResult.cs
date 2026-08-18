namespace LendingLibrary.Web.Services.Abstractions;

public enum CatalogueOperationOutcome
{
    Success,
    NotFound,
    DuplicateIsbn,
    ConcurrencyConflict,
    ValidationFailed
}

public class CatalogueOperationResult
{
    public required CatalogueOperationOutcome Outcome { get; init; }

    public string? Error { get; init; }

    public Guid? Id { get; init; }

    public bool Succeeded => Outcome == CatalogueOperationOutcome.Success;

    public static CatalogueOperationResult Success(Guid id) =>
        new() { Outcome = CatalogueOperationOutcome.Success, Id = id };

    public static CatalogueOperationResult Failure(CatalogueOperationOutcome outcome, string error) =>
        new() { Outcome = outcome, Error = error };
}
