using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class PendingPaperConfiguration : IEntityTypeConfiguration<PendingPaper>
{
    public void Configure(EntityTypeBuilder<PendingPaper> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ExternalId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.ExternalSource)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.Abstract)
            .HasMaxLength(5000);

        builder.Property(p => p.Doi)
            .HasMaxLength(100);

        builder.Property(p => p.Url)
            .HasMaxLength(500);

        builder.Property(p => p.AuthorNamesJson)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(p => new { p.ExternalId, p.ExternalSource });
        builder.HasIndex(p => p.Status);
    }
}
