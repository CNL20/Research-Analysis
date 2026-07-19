using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class PaperAnalysisConfiguration : IEntityTypeConfiguration<PaperAnalysis>
{
    public void Configure(EntityTypeBuilder<PaperAnalysis> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.PaperId).IsUnique();
        
        builder.Property(x => x.AnalysisLevel).HasMaxLength(20);
        builder.Property(x => x.AnalysisSource).HasMaxLength(50);
        
        builder.Property(x => x.MethodsJson).HasColumnType("text");
        builder.Property(x => x.DatasetsJson).HasColumnType("text");
        builder.Property(x => x.LimitationsJson).HasColumnType("text");
        builder.Property(x => x.FutureWorkJson).HasColumnType("text");
        builder.Property(x => x.DiscussionsJson).HasColumnType("text");
        builder.Property(x => x.ConclusionsJson).HasColumnType("text");
        builder.Property(x => x.KeywordsJson).HasColumnType("text");

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
