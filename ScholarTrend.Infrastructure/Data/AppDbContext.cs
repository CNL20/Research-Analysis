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

    #region Topic Insights

    public DbSet<PaperTopicExtraction> PaperTopicExtractions => Set<PaperTopicExtraction>();
    public DbSet<TopicInsight> TopicInsights => Set<TopicInsight>();
    public DbSet<TopicInsightEvidence> TopicInsightEvidences => Set<TopicInsightEvidence>();
    public DbSet<TopicInsightJob> TopicInsightJobs => Set<TopicInsightJob>();

    #endregion

    #region User Interaction

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<FollowedTopic> FollowedTopics => Set<FollowedTopic>();
    public DbSet<FollowedJournal> FollowedJournals => Set<FollowedJournal>();
    public DbSet<FollowedAuthor> FollowedAuthors => Set<FollowedAuthor>();
    public DbSet<FollowedPaper> FollowedPapers => Set<FollowedPaper>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SearchHistory> SearchHistories => Set<SearchHistory>();
    public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserFile> UserFiles => Set<UserFile>();

    #endregion

    #region System Support

    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<ApiDataSource> ApiDataSources => Set<ApiDataSource>();
    public DbSet<SyncProposal> SyncProposals => Set<SyncProposal>();
    public DbSet<PendingPaper> PendingPapers => Set<PendingPaper>();
    public DbSet<PaperPdfFile> PaperPdfFiles => Set<PaperPdfFile>();

    #endregion

    #region Paper Analysis & Quality

    public DbSet<PaperQuality> PaperQualities => Set<PaperQuality>();
    public DbSet<PaperAnalysis> PaperAnalyses => Set<PaperAnalysis>();
    public DbSet<AnalysisJob> AnalysisJobs => Set<AnalysisJob>();

    #endregion

    #region Pattern Mining

    public DbSet<MethodPattern> MethodPatterns => Set<MethodPattern>();
    public DbSet<DatasetPattern> DatasetPatterns => Set<DatasetPattern>();
    public DbSet<LimitationPattern> LimitationPatterns => Set<LimitationPattern>();

    #endregion

    #region Research Gap

    public DbSet<ResearchGap> ResearchGaps => Set<ResearchGap>();
    public DbSet<ResearchGapEvidence> ResearchGapEvidences => Set<ResearchGapEvidence>();
    public DbSet<GapTimeline> GapTimelines => Set<GapTimeline>();
    public DbSet<CoverageReport> CoverageReports => Set<CoverageReport>();

    #endregion

    #region Payment

    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PaymentWebhookLog> PaymentWebhookLogs => Set<PaymentWebhookLog>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Invoice> Invoices => Set<Invoice>();

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Enable PostgreSQL trgm extension for fast ILIKE searches
        modelBuilder.HasPostgresExtension("pg_trgm");

        // Apply entity configurations from assembly
        // All IEntityTypeConfiguration implementations will be auto-loaded
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScholarTrendDbContext).Assembly);
    }
}
