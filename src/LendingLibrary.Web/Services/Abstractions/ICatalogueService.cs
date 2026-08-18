using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;

namespace LendingLibrary.Web.Services.Abstractions;

public interface ICatalogueService
{
    Task<PagedResult<CatalogueItem>> SearchAsync(
        CatalogueSearchQuery query, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CatalogueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CatalogueOperationResult> CreateAsync(CatalogueItemInput input, CancellationToken cancellationToken = default);

    Task<CatalogueOperationResult> UpdateAsync(
        Guid id, CatalogueItemInput input, uint rowVersion, CancellationToken cancellationToken = default);

    Task<CatalogueOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
