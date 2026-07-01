using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> builder)
    {
        // Primary Key
        builder.HasKey(k => k.Id);

        // Properties
        builder.Property(k => k.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(k => k.CreatedAt)
            .HasDefaultValueSql("timezone('utc', now())");

        // Indexes
        builder.HasIndex(k => k.Name)
            .IsUnique()
            .HasDatabaseName("IX_Keyword_Name");

        builder.HasIndex(k => k.CreatedAt)
            .HasDatabaseName("IX_Keyword_CreatedAt");

        // Relationships
        builder.HasMany(k => k.PaperKeywords)
            .WithOne(pk => pk.Keyword)
            .HasForeignKey(pk => pk.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(k => k.KeywordTrends)
            .WithOne(kt => kt.Keyword)
            .HasForeignKey(kt => kt.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
