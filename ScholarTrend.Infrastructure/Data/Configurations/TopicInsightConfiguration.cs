using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class TopicInsightConfiguration : IEntityTypeConfiguration<TopicInsight>
{
    public void Configure(EntityTypeBuilder<TopicInsight> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(x => x.Evidences)
            .WithOne(e => e.TopicInsight)
            .HasForeignKey(e => e.TopicInsightId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
