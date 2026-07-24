using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class KeywordTrendConfiguration : IEntityTypeConfiguration<KeywordTrend>
{
    public void Configure(EntityTypeBuilder<KeywordTrend> builder)
    {
        builder.HasIndex(t => new { t.KeywordId, t.Year, t.Month })
            .IsUnique()
            .HasDatabaseName("IX_KeywordTrends_KeywordId_Year_Month");
            
        builder.HasIndex(t => new { t.Year, t.Month })
            .HasDatabaseName("IX_KeywordTrends_Year_Month");
            
        builder.HasIndex(t => t.TrendingScore)
            .HasDatabaseName("IX_KeywordTrends_TrendingScore");
    }
}

public class TopicTrendConfiguration : IEntityTypeConfiguration<TopicTrend>
{
    public void Configure(EntityTypeBuilder<TopicTrend> builder)
    {
        builder.HasIndex(t => new { t.TopicId, t.Year, t.Month })
            .IsUnique()
            .HasDatabaseName("IX_TopicTrends_TopicId_Year_Month");
            
        builder.HasIndex(t => new { t.Year, t.Month })
            .HasDatabaseName("IX_TopicTrends_Year_Month");
            
        builder.HasIndex(t => t.TrendingScore)
            .HasDatabaseName("IX_TopicTrends_TrendingScore");
    }
}

public class JournalTrendConfiguration : IEntityTypeConfiguration<JournalTrend>
{
    public void Configure(EntityTypeBuilder<JournalTrend> builder)
    {
        builder.HasIndex(t => new { t.JournalId, t.Year, t.Month })
            .IsUnique()
            .HasDatabaseName("IX_JournalTrends_JournalId_Year_Month");
            
        builder.HasIndex(t => new { t.Year, t.Month })
            .HasDatabaseName("IX_JournalTrends_Year_Month");
            
        builder.HasIndex(t => t.TrendingScore)
            .HasDatabaseName("IX_JournalTrends_TrendingScore");
    }
}
