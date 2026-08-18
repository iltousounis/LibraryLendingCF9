using LendingLibrary.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LendingLibrary.Web.Data.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(l => new { l.UserId, l.Status });
        builder.HasIndex(l => l.DueAtUtc);

        builder.HasOne(l => l.CatalogueItem)
            .WithMany(i => i.Loans)
            .HasForeignKey(l => l.CatalogueItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.User)
            .WithMany(u => u.Loans)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
