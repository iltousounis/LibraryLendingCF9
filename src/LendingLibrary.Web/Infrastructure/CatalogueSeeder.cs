using LendingLibrary.Web.Data;
using LendingLibrary.Web.Domain.Entities;
using LendingLibrary.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LendingLibrary.Web.Infrastructure;

/// <summary>Seeds a starter catalogue (with cover art) into an empty database.</summary>
public static class CatalogueSeeder
{
    public static async Task SeedAsync(AppDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken = default)
    {
        if (await db.CatalogueItems.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        CatalogueItem Item(
            string title, ItemType itemType, string? authors, string? publisher, string? isbn,
            int? publicationYear, string? description, string coverImageUrl, int totalUnits, int availableUnits) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            ItemType = itemType,
            Authors = authors,
            Publisher = publisher,
            Isbn = isbn,
            PublicationYear = publicationYear,
            Description = description,
            CoverImageUrl = coverImageUrl,
            TotalUnits = totalUnits,
            AvailableUnits = availableUnits,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.CatalogueItems.AddRange(
            Item("1984", ItemType.Book, "George Orwell", null, null, 1949, null,
                "https://covers.openlibrary.org/b/id/9267242-L.jpg", 1, 1),
            Item("Brave New World", ItemType.Book, "Aldous Huxley", "Chatto & Windus", "9780060850524", 1932,
                "A dystopian vision of a future society engineered for stability through pleasure, conditioning, and control.",
                "https://covers.openlibrary.org/b/id/8231823-L.jpg", 2, 2),
            Item("Dune", ItemType.Book, "Frank Herbert", null, null, 1965, null,
                "https://covers.openlibrary.org/b/id/11481354-L.jpg", 2, 2),
            Item("Fahrenheit 451", ItemType.Book, "Ray Bradbury", "Ballantine Books", "9781451673319", 1953,
                "In a future where books are outlawed and burned, a fireman begins to question his role.",
                "https://covers.openlibrary.org/b/id/12993656-L.jpg", 2, 2),
            Item("Foundation", ItemType.Book, "Isaac Asimov", null, null, 1951, null,
                "https://covers.openlibrary.org/b/id/14612610-L.jpg", 0, 0),
            Item("Pride and Prejudice", ItemType.Book, "Jane Austen", "T. Egerton", "9780141439518", 1813,
                "Elizabeth Bennet navigates manners, morality, and marriage in Regency England.",
                "https://covers.openlibrary.org/b/id/14348537-L.jpg", 2, 2),
            Item("Slaughterhouse-Five", ItemType.Book, "Kurt Vonnegut", "Delacorte Press", "9780385333849", 1969,
                "A satirical, time-unstuck account of soldier Billy Pilgrim's experience of the firebombing of Dresden.",
                "https://covers.openlibrary.org/b/id/12727001-L.jpg", 1, 1),
            Item("The Great Gatsby", ItemType.Book, "F. Scott Fitzgerald", "Charles Scribner's Sons", "9780743273565", 1925,
                "Jay Gatsby's obsessive pursuit of a lost love unravels amid the excess of the Jazz Age.",
                "https://covers.openlibrary.org/b/id/10590366-L.jpg", 3, 3),
            Item("The Hobbit", ItemType.Book, "J.R.R. Tolkien", null, null, 1937, null,
                "https://covers.openlibrary.org/b/id/14627509-L.jpg", 3, 3),
            Item("The Matrix", ItemType.Dvd, null, null, null, 1999, null,
                "https://upload.wikimedia.org/wikipedia/en/d/db/The_Matrix.png", 2, 2),
            Item("To Kill a Mockingbird", ItemType.Book, "Harper Lee", "J. B. Lippincott & Co.", "9780060935467", 1960,
                "A young girl in the Depression-era South confronts prejudice through her father's defense of a wrongly accused Black man.",
                "https://covers.openlibrary.org/b/id/14351077-L.jpg", 3, 3));

        await db.SaveChangesAsync(cancellationToken);
    }
}
