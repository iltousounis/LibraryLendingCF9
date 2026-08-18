using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Infrastructure;
using LendingLibrary.Web.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LendingLibrary.Web.Services.Implementations;

public class CatalogueService(AppDbContext db, TimeProvider timeProvider) : ICatalogueService
{
    public async Task<PagedResult<CatalogueItem>> SearchAsync(
        CatalogueSearchQuery query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var items = db.CatalogueItems.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var term = $"%{query.SearchText.Trim()}%";
            items = items.Where(i =>
                EF.Functions.ILike(i.Title, term) ||
                (i.Authors != null && EF.Functions.ILike(i.Authors, term)) ||
                (i.Isbn != null && EF.Functions.ILike(i.Isbn, term)) ||
                (i.Publisher != null && EF.Functions.ILike(i.Publisher, term)));
        }

        if (query.ItemType is { } itemType)
        {
            items = items.Where(i => i.ItemType == itemType);
        }

        if (query.PublicationYearFrom is { } yearFrom)
        {
            items = items.Where(i => i.PublicationYear >= yearFrom);
        }

        if (query.PublicationYearTo is { } yearTo)
        {
            items = items.Where(i => i.PublicationYear <= yearTo);
        }

        if (!string.IsNullOrWhiteSpace(query.Publisher))
        {
            var publisherTerm = $"%{query.Publisher.Trim()}%";
            items = items.Where(i => i.Publisher != null && EF.Functions.ILike(i.Publisher, publisherTerm));
        }

        if (query.AvailableOnly == true)
        {
            items = items.Where(i => i.AvailableUnits > 0);
        }

        items = items.OrderBy(i => i.Title);

        var totalCount = await items.CountAsync(cancellationToken);
        var pageItems = await items.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<CatalogueItem>(pageItems, page, pageSize, totalCount);
    }

    public Task<CatalogueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.CatalogueItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<CatalogueOperationResult> CreateAsync(CatalogueItemInput input, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var entity = new CatalogueItem
        {
            Id = Guid.NewGuid(),
            Title = input.Title.Trim(),
            ItemType = input.ItemType,
            Authors = Normalize(input.Authors),
            Publisher = Normalize(input.Publisher),
            Isbn = Normalize(input.Isbn),
            PublicationYear = input.PublicationYear,
            Description = Normalize(input.Description),
            CoverImageUrl = Normalize(input.CoverImageUrl),
            TotalUnits = input.TotalUnits,
            AvailableUnits = input.TotalUnits,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.CatalogueItems.Add(entity);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return CatalogueOperationResult.Failure(CatalogueOperationOutcome.DuplicateIsbn, "An item with this ISBN already exists.");
        }

        return CatalogueOperationResult.Success(entity.Id);
    }

    public async Task<CatalogueOperationResult> UpdateAsync(
        Guid id, CatalogueItemInput input, uint rowVersion, CancellationToken cancellationToken = default)
    {
        var entity = await db.CatalogueItems.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (entity is null)
        {
            return CatalogueOperationResult.Failure(CatalogueOperationOutcome.NotFound, "Item not found.");
        }

        var onLoan = entity.TotalUnits - entity.AvailableUnits;
        var newAvailable = input.TotalUnits - onLoan;
        if (newAvailable < 0)
        {
            return CatalogueOperationResult.Failure(
                CatalogueOperationOutcome.ValidationFailed,
                $"Cannot reduce total units below the {onLoan} currently on loan.");
        }

        db.Entry(entity).Property(e => e.RowVersion).OriginalValue = rowVersion;

        entity.Title = input.Title.Trim();
        entity.ItemType = input.ItemType;
        entity.Authors = Normalize(input.Authors);
        entity.Publisher = Normalize(input.Publisher);
        entity.Isbn = Normalize(input.Isbn);
        entity.PublicationYear = input.PublicationYear;
        entity.Description = Normalize(input.Description);
        entity.CoverImageUrl = Normalize(input.CoverImageUrl);
        entity.TotalUnits = input.TotalUnits;
        entity.AvailableUnits = newAvailable;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return CatalogueOperationResult.Failure(
                CatalogueOperationOutcome.ConcurrencyConflict,
                "This item was changed by someone else. Reload and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return CatalogueOperationResult.Failure(CatalogueOperationOutcome.DuplicateIsbn, "An item with this ISBN already exists.");
        }

        return CatalogueOperationResult.Success(entity.Id);
    }

    public async Task<CatalogueOperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.CatalogueItems.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (entity is null)
        {
            return CatalogueOperationResult.Failure(CatalogueOperationOutcome.NotFound, "Item not found.");
        }

        entity.DeletedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return CatalogueOperationResult.Success(entity.Id);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
