using LendingLibrary.Web.Domain.Enums;

namespace LendingLibrary.Web.Services.Abstractions;

public record CatalogueItemInput(
    string Title,
    ItemType ItemType,
    string? Authors,
    string? Publisher,
    string? Isbn,
    int? PublicationYear,
    string? Description,
    string? CoverImageUrl,
    int TotalUnits);
