using LendingLibrary.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LendingLibrary.Web.Data.Configurations;

public class CatalogueItemConfiguration : IEntityTypeConfiguration<CatalogueItem>
{
    public void Configure(EntityTypeBuilder<CatalogueItem> builder)
    {
        builder.Property(i => i.Title).IsRequired().HasMaxLength(500);
        builder.Property(i => i.Authors).HasMaxLength(500);
        builder.Property(i => i.Publisher).HasMaxLength(200);
        builder.Property(i => i.Isbn).HasMaxLength(20);
        builder.Property(i => i.ItemType).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(i => i.Title);
        builder.HasIndex(i => i.Isbn).IsUnique().HasFilter("\"Isbn\" IS NOT NULL");
        builder.HasIndex(i => i.Publisher);
        builder.HasIndex(i => i.PublicationYear);

        builder.Property(i => i.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();
    }
}
