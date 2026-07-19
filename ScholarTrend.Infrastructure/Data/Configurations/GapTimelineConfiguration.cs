using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class GapTimelineConfiguration : IEntityTypeConfiguration<GapTimeline>
{
    public void Configure(EntityTypeBuilder<GapTimeline> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TopicId, x.Year, x.GapType });
        
        builder.Property(x => x.GapType).HasMaxLength(50);
        builder.Property(x => x.GapTitle).HasMaxLength(500);
        builder.Property(x => x.Trend).HasMaxLength(20);

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CoverageReportConfiguration : IEntityTypeConfiguration<CoverageReport>
{
    public void Configure(EntityTypeBuilder<CoverageReport> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TopicId);
        
        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
