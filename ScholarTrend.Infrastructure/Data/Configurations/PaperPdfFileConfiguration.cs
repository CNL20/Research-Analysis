using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class PaperPdfFileConfiguration : IEntityTypeConfiguration<PaperPdfFile>
{
    public void Configure(EntityTypeBuilder<PaperPdfFile> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ExternalSource)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.SourceUrl)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(p => p.LocalRelativePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.ContentType)
            .HasMaxLength(100);

        builder.Property(p => p.Sha256)
            .HasMaxLength(64);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.FailureReason)
            .HasMaxLength(1000);

        builder.HasOne(p => p.ResearchPaper)
            .WithMany()
            .HasForeignKey(p => p.ResearchPaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.ResearchPaperId).IsUnique();
        builder.HasIndex(p => p.Status);
    }
}
