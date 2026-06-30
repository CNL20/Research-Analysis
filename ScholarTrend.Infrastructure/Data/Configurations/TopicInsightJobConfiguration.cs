using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class TopicInsightJobConfiguration : IEntityTypeConfiguration<TopicInsightJob>
{
    public void Configure(EntityTypeBuilder<TopicInsightJob> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Property(x => x.Status).HasMaxLength(50);
    }
}
