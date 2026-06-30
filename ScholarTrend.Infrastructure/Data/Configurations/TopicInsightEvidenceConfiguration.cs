using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class TopicInsightEvidenceConfiguration : IEntityTypeConfiguration<TopicInsightEvidence>
{
    public void Configure(EntityTypeBuilder<TopicInsightEvidence> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Property(x => x.EvidenceType).HasMaxLength(50);
    }
}
