using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class PaperTopicExtractionConfiguration : IEntityTypeConfiguration<PaperTopicExtraction>
{
    public void Configure(EntityTypeBuilder<PaperTopicExtraction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
