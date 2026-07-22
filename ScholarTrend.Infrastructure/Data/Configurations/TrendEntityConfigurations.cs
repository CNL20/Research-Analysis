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
    }
}

public class TopicTrendConfiguration : IEntityTypeConfiguration<TopicTrend>
{
    public void Configure(EntityTypeBuilder<TopicTrend> builder)
    {
        builder.HasIndex(t => new { t.TopicId, t.Year, t.Month })
            .IsUnique()
            .HasDatabaseName("IX_TopicTrends_TopicId_Year_Month");
    }
}

public class JournalTrendConfiguration : IEntityTypeConfiguration<JournalTrend>
{
    public void Configure(EntityTypeBuilder<JournalTrend> builder)
    {
        builder.HasIndex(t => new { t.JournalId, t.Year, t.Month })
            .IsUnique()
            .HasDatabaseName("IX_JournalTrends_JournalId_Year_Month");
    }
}
