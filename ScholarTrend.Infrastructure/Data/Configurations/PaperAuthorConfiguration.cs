using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class PaperAuthorConfiguration : IEntityTypeConfiguration<PaperAuthor>
{
    public void Configure(EntityTypeBuilder<PaperAuthor> builder)
    {
        // Composite Primary Key
        builder.HasKey(pa => new { pa.PaperId, pa.AuthorId });

        builder.Property(pa => pa.AuthorOrder)
            .HasDefaultValue(0);

        // Indexes for queries
        builder.HasIndex(pa => pa.AuthorId)
            .HasDatabaseName("IX_PaperAuthor_AuthorId");

        // Relationships
        builder.HasOne(pa => pa.Paper)
            .WithMany(p => p.PaperAuthors)
            .HasForeignKey(pa => pa.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pa => pa.Author)
            .WithMany(a => a.PaperAuthors)
            .HasForeignKey(pa => pa.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
