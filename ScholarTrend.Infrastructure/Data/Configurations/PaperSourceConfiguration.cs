using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class PaperSourceConfiguration : IEntityTypeConfiguration<PaperSource>
{
    public void Configure(EntityTypeBuilder<PaperSource> builder)
    {
        builder.ToTable("PaperSources");

        builder.HasKey(x => new { x.PaperId, x.SourceName });

        builder.Property(x => x.SourceName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ExternalId)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.SourceDoi)
            .HasMaxLength(200);

        builder.Property(x => x.SourceUrl)
            .HasMaxLength(1000);

        builder.Property(x => x.RawMetadataJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.FetchedAt)
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.LastSeenAt)
            .HasDefaultValueSql("timezone('utc', now())");

        builder.HasOne(x => x.Paper)
            .WithMany(p => p.PaperSources)
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SourceDoi)
            .HasDatabaseName("IX_PaperSources_SourceDoi");

        builder.HasIndex(x => x.ExternalId)
            .HasDatabaseName("IX_PaperSources_ExternalId");

        builder.HasIndex(x => x.SourceName)
            .HasDatabaseName("IX_PaperSources_SourceName");
    }
}
