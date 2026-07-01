using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class UserFileConfiguration : IEntityTypeConfiguration<UserFile>
{
    public void Configure(EntityTypeBuilder<UserFile> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.OriginalFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.StoredFileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(f => f.ContentType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.Category)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(f => f.Description)
            .HasMaxLength(500);

        builder.HasIndex(f => f.UserId);
        builder.HasIndex(f => new { f.UserId, f.Category });
        builder.HasIndex(f => f.PaperId);

        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Paper)
            .WithMany()
            .HasForeignKey(f => f.PaperId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
