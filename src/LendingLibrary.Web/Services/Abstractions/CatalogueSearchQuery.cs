using LendingLibrary.Web.Domain.Enums;

namespace LendingLibrary.Web.Services.Abstractions;

public record CatalogueSearchQuery(
    string? SearchText = null,
    ItemType? ItemType = null,
    int? PublicationYearFrom = null,
    int? PublicationYearTo = null,
    string? Publisher = null,
    bool? AvailableOnly = null);
