using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class FollowedAuthorConfiguration : IEntityTypeConfiguration<FollowedAuthor>
{
    public void Configure(EntityTypeBuilder<FollowedAuthor> builder)
    {
        builder.HasKey(f => f.Id);

        builder.HasIndex(f => f.UserId);
        builder.HasIndex(f => f.AuthorId);
        builder.HasIndex(f => new { f.UserId, f.AuthorId }).IsUnique();

        builder.HasOne(f => f.User)
            .WithMany(u => u.FollowedAuthors)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Author)
            .WithMany(a => a.FollowedAuthors)
            .HasForeignKey(f => f.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
