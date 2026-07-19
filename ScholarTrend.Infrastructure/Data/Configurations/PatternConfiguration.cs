using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data.Configurations;

public class MethodPatternConfiguration : IEntityTypeConfiguration<MethodPattern>
{
    public void Configure(EntityTypeBuilder<MethodPattern> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TopicId, x.MethodName, x.Year });
        
        builder.Property(x => x.MethodName).HasMaxLength(200);

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DatasetPatternConfiguration : IEntityTypeConfiguration<DatasetPattern>
{
    public void Configure(EntityTypeBuilder<DatasetPattern> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TopicId, x.DatasetName, x.Year });
        
        builder.Property(x => x.DatasetName).HasMaxLength(200);

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LimitationPatternConfiguration : IEntityTypeConfiguration<LimitationPattern>
{
    public void Configure(EntityTypeBuilder<LimitationPattern> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TopicId, x.LimitationText, x.Year });
        
        builder.Property(x => x.LimitationText).HasColumnType("text");

        builder.HasOne(x => x.Topic)
            .WithMany()
            .HasForeignKey(x => x.TopicId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
