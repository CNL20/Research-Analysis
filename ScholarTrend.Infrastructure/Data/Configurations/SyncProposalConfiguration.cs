using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class SyncProposalConfiguration : IEntityTypeConfiguration<SyncProposal>
{
    public void Configure(EntityTypeBuilder<SyncProposal> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.ReviewedByUserId)
            .HasMaxLength(450);

        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt);

        builder.HasMany(p => p.PendingPapers)
            .WithOne(pp => pp.SyncProposal)
            .HasForeignKey(pp => pp.SyncProposalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
