using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class ResearchPaperConfiguration : IEntityTypeConfiguration<ResearchPaper>
{
    public void Configure(EntityTypeBuilder<ResearchPaper> builder)
    {
        // Primary Key
        builder.HasKey(p => p.Id);

        // Properties configuration
        builder.Property(p => p.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(p => p.Abstract)
            .HasMaxLength(5000);

        builder.Property(p => p.Doi)
            .HasMaxLength(100);

        builder.Property(p => p.Url)
            .HasMaxLength(500);

        builder.Property(p => p.PdfUrl)
            .HasMaxLength(500);

        builder.Property(p => p.PublicationYear);

        builder.Property(p => p.PublicationDate);

        builder.Property(p => p.CitationCount)
            .HasDefaultValue(0);

        builder.Property(p => p.Status)
            .HasDefaultValue(Domain.Enums.PaperStatus.Fetched);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(p => p.UpdatedAt);

        // Indexes - Critical for search & filtering
        builder.HasIndex(p => p.Title)
            .HasDatabaseName("IX_ResearchPaper_Title");

        builder.HasIndex(p => p.PublicationYear)
            .HasDatabaseName("IX_ResearchPaper_PublicationYear");

        builder.HasIndex(p => p.CitationCount)
            .HasDatabaseName("IX_ResearchPaper_CitationCount");

        builder.HasIndex(p => p.CreatedAt)
            .HasDatabaseName("IX_ResearchPaper_CreatedAt");

        // Relationships
        builder.HasOne(p => p.Journal)
            .WithMany(j => j.Papers)
            .HasForeignKey(p => p.JournalId)
            .OnDelete(DeleteBehavior.SetNull);

        // Collection navigations
        builder.HasMany(p => p.PaperAuthors)
            .WithOne(pa => pa.Paper)
            .HasForeignKey(pa => pa.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.PaperKeywords)
            .WithOne(pk => pk.Paper)
            .HasForeignKey(pk => pk.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.PaperTopics)
            .WithOne(pt => pt.Paper)
            .HasForeignKey(pt => pt.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Bookmarks)
            .WithOne(b => b.Paper)
            .HasForeignKey(b => b.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.PaperSources)
            .WithOne(ps => ps.Paper)
            .HasForeignKey(ps => ps.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasOne(p => p.Analysis)
            .WithOne(a => a.Paper)
            .HasForeignKey<PaperAnalysis>(a => a.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
