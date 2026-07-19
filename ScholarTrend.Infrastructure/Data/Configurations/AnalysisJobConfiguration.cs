using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class AnalysisJobConfiguration : IEntityTypeConfiguration<AnalysisJob>
{
    public void Configure(EntityTypeBuilder<AnalysisJob> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.PaperId);
        builder.HasIndex(x => x.Status);
        
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.AnalysisType).HasMaxLength(20);

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
