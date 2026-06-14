using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Infrastructure.Data;

public class ScholarTrendDbContext : IdentityDbContext<User>
{
    public ScholarTrendDbContext(DbContextOptions<ScholarTrendDbContext> options)
        : base(options)
    {
    }

    #region Core Entities

    public DbSet<ResearchPaper> ResearchPapers => Set<ResearchPaper>();
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Journal> Journals => Set<Journal>();
    public DbSet<Keyword> Keywords => Set<Keyword>();
    public DbSet<ResearchTopic> ResearchTopics => Set<ResearchTopic>();

    #endregion

    #region Relationship Entities

    public DbSet<PaperAuthor> PaperAuthors => Set<PaperAuthor>();
    public DbSet<PaperKeyword> PaperKeywords => Set<PaperKeyword>();
    public DbSet<PaperTopic> PaperTopics => Set<PaperTopic>();

    #endregion

    #region Trend Entities

    public DbSet<KeywordTrend> KeywordTrends => Set<KeywordTrend>();
    public DbSet<TopicTrend> TopicTrends => Set<TopicTrend>();
    public DbSet<JournalTrend> JournalTrends => Set<JournalTrend>();

    #endregion

    #region User Interaction

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<FollowedTopic> FollowedTopics => Set<FollowedTopic>();
    public DbSet<FollowedJournal> FollowedJournals => Set<FollowedJournal>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SearchHistory> SearchHistories => Set<SearchHistory>();
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    #endregion

    #region System Support

    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<ApiDataSource> ApiDataSources => Set<ApiDataSource>();
    public DbSet<SyncProposal> SyncProposals => Set<SyncProposal>();
    public DbSet<PendingPaper> PendingPapers => Set<PendingPaper>();

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations from assembly
        // All IEntityTypeConfiguration implementations will be auto-loaded
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScholarTrendDbContext).Assembly);
    }
}
