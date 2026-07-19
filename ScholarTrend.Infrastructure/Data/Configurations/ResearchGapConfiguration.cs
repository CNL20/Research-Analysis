using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class ResearchGapConfiguration : IEntityTypeConfiguration<ResearchGap>
{
    public void Configure(EntityTypeBuilder<ResearchGap> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TopicId);
        builder.HasIndex(x => x.GapType);
        
        builder.Property(x => x.Title).HasMaxLength(500);
        builder.Property(x => x.GapType).HasMaxLength(50);
        builder.Property(x => x.ConfidenceLevel).HasMaxLength(20);
        builder.Property(x => x.Description).HasColumnType("text");
        builder.Property(x => x.SuggestedDirection).HasColumnType("text");

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ResearchGapEvidenceConfiguration : IEntityTypeConfiguration<ResearchGapEvidence>
{
    public void Configure(EntityTypeBuilder<ResearchGapEvidence> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ResearchGapId);
        builder.HasIndex(x => x.PaperId);
        
        builder.Property(x => x.EvidenceSentence).HasColumnType("text");
        builder.Property(x => x.EvidenceType).HasMaxLength(50);
        builder.Property(x => x.SectionSource).HasMaxLength(50);
        builder.Property(x => x.PageContext).HasColumnType("text");
        builder.Property(x => x.ValidationStatus).HasMaxLength(20);

        builder.HasOne(x => x.ResearchGap)
            .WithMany(x => x.Evidences)
            .HasForeignKey(x => x.ResearchGapId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
