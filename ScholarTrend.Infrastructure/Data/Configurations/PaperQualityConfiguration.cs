using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class PaperQualityConfiguration : IEntityTypeConfiguration<PaperQuality>
{
    public void Configure(EntityTypeBuilder<PaperQuality> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.PaperId).IsUnique();
        
        builder.Property(x => x.QualityGrade).HasMaxLength(10);
        builder.Property(x => x.AnalysisLevel).HasMaxLength(20);

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
