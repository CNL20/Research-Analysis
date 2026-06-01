using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class PaperTopicConfiguration : IEntityTypeConfiguration<PaperTopic>
{
    public void Configure(EntityTypeBuilder<PaperTopic> builder)
    {
        builder.HasKey(pt => new { pt.PaperId, pt.TopicId });

        builder.HasIndex(pt => pt.TopicId)
            .HasDatabaseName("IX_PaperTopic_TopicId");

        builder.HasOne(pt => pt.Paper)
            .WithMany(p => p.PaperTopics)
            .HasForeignKey(pt => pt.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pt => pt.Topic)
            .WithMany(t => t.PaperTopics)
            .HasForeignKey(pt => pt.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
