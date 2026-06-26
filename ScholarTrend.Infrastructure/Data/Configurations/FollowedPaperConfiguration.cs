using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class FollowedPaperConfiguration : IEntityTypeConfiguration<FollowedPaper>
{
    public void Configure(EntityTypeBuilder<FollowedPaper> builder)
    {
        builder.HasKey(f => f.Id);

        builder.HasIndex(f => f.UserId);
        builder.HasIndex(f => f.PaperId);
        builder.HasIndex(f => new { f.UserId, f.PaperId }).IsUnique();

        builder.HasOne(f => f.User)
            .WithMany(u => u.FollowedPapers)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Paper)
            .WithMany(p => p.FollowedPapers)
            .HasForeignKey(f => f.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
