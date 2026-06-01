using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Configurations;

public class PaperKeywordConfiguration : IEntityTypeConfiguration<PaperKeyword>
{
    public void Configure(EntityTypeBuilder<PaperKeyword> builder)
    {
        builder.HasKey(pk => new { pk.PaperId, pk.KeywordId });

        builder.HasIndex(pk => pk.KeywordId)
            .HasDatabaseName("IX_PaperKeyword_KeywordId");

        builder.HasOne(pk => pk.Paper)
            .WithMany(p => p.PaperKeywords)
            .HasForeignKey(pk => pk.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pk => pk.Keyword)
            .WithMany(k => k.PaperKeywords)
            .HasForeignKey(pk => pk.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
