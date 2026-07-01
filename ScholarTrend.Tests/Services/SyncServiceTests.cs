using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ScholarTrend.Application.DTOs.Sync;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Tests.Services;

public class SyncServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ISyncProposalRepository> _mockSyncProposalRepo;
    private readonly Mock<IPaperImportRepository> _mockPaperImportRepo;
    private readonly Mock<ISemanticScholarClient> _mockSemanticClient;
    private readonly Mock<IOpenAlexClient> _mockOpenAlexClient;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<SyncService>> _mockLogger;
    private readonly SyncService _syncService;

    public SyncServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockSyncProposalRepo = new Mock<ISyncProposalRepository>();
        _mockPaperImportRepo = new Mock<IPaperImportRepository>();
        _mockSemanticClient = new Mock<ISemanticScholarClient>();
        _mockOpenAlexClient = new Mock<IOpenAlexClient>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<SyncService>>();

        _mockConfig.Setup(c => c["ExternalApis:SemanticScholar:SearchQuery"]).Returns("artificial intelligence");

        _mockUnitOfWork.Setup(u => u.SyncProposals).Returns(_mockSyncProposalRepo.Object);

        _syncService = new SyncService(
            _mockUnitOfWork.Object,
            _mockPaperImportRepo.Object,
            _mockSemanticClient.Object,
            _mockOpenAlexClient.Object,
            _mockNotificationService.Object,
            _mockConfig.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task RunSyncAsync_ShouldCreatePendingProposal_AndNotifyAdmins()
    {
        var source = new ApiDataSource { Id = 1, Name = "SemanticScholar", IsActive = true };
        _mockUnitOfWork.Setup(u => u.ApiDataSources.GetActiveAsync())
            .ReturnsAsync(new List<ApiDataSource> { source });
        _mockUnitOfWork.Setup(u => u.SyncLogs.AddAsync(It.IsAny<SyncLog>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.Journals.GetAllAsync())
            .ReturnsAsync(new List<Journal> { new() { Id = 1 } });
        _mockSyncProposalRepo.Setup(r => r.AddAsync(It.IsAny<SyncProposal>()))
            .Callback<SyncProposal>(p => p.Id = 101)
            .Returns(Task.CompletedTask);
        _mockSyncProposalRepo.Setup(r => r.IsPaperAlreadyQueuedOrStoredAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var externalPapers = new List<ExternalPaperDto>
        {
            new() { ExternalId = "ext1", Title = "Paper 1", Source = "SemanticScholar", Doi = "10.123/1" }
        };

        _mockSemanticClient.Setup(c => c.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(externalPapers);

        var result = await _syncService.RunSyncAsync("SemanticScholar");

        result.Results.Should().HaveCount(1);
        var syncResult = result.Results[0];
        syncResult.Status.Should().Be("Completed");
        syncResult.PapersAdded.Should().Be(1);
        syncResult.SyncProposalId.Should().Be(101);
        _mockPaperImportRepo.Verify(r => r.ImportAsync(It.IsAny<ExternalPaperDto>(), It.IsAny<int?>()), Times.Never);
        _mockNotificationService.Verify(n => n.NotifyAdminsPendingSyncAsync(101, 1), Times.Once);
        _mockNotificationService.Verify(n => n.NotifyFollowersForNewPaperAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ApprovePendingSyncAsync_ShouldImportPapers_AndNotifyFollowers()
    {
        var proposal = new SyncProposal
        {
            Id = 101,
            Status = SyncProposalStatus.Pending,
            PendingPapers =
            [
                new PendingPaper
                {
                    Id = 1,
                    ExternalId = "ext1",
                    ExternalSource = "SemanticScholar",
                    Title = "Paper 1",
                    AuthorNamesJson = "[]",
                    Status = PendingPaperStatus.Pending
                }
            ]
        };

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(101)).ReturnsAsync(proposal);
        _mockUnitOfWork.Setup(u => u.Journals.GetAllAsync())
            .ReturnsAsync(new List<Journal> { new() { Id = 1 } });
        _mockPaperImportRepo.Setup(r => r.ImportAsync(It.IsAny<ExternalPaperDto>(), It.IsAny<int?>()))
            .ReturnsAsync(new Application.Interfaces.Repositories.ResearchPaperImportResult { IsNew = true, PaperId = 501 });

        var result = await _syncService.ApprovePendingSyncAsync(101, "admin-id", new ApproveSyncRequest());

        result.Status.Should().Be(SyncProposalStatus.Approved);
        result.PapersApproved.Should().Be(1);
        _mockPaperImportRepo.Verify(r => r.ImportAsync(It.IsAny<ExternalPaperDto>(), It.IsAny<int?>()), Times.Once);
        _mockNotificationService.Verify(n => n.NotifyFollowersForNewPaperAsync(501), Times.Once);
    }

    [Fact]
    public async Task RunSyncAsync_ShouldHandleApiFailure_Gracefully()
    {
        var source = new ApiDataSource { Id = 1, Name = "SemanticScholar", IsActive = true };
        _mockUnitOfWork.Setup(u => u.ApiDataSources.GetActiveAsync())
            .ReturnsAsync(new List<ApiDataSource> { source });
        _mockUnitOfWork.Setup(u => u.SyncLogs.AddAsync(It.IsAny<SyncLog>()))
            .Returns(Task.CompletedTask);
        // Make the flow fail *after* fetching — by returning no journals — so the
        // outer catch in SyncSingleQueryAsync records a real failure message.
        _mockUnitOfWork.Setup(u => u.Journals.GetAllAsync())
            .ReturnsAsync(new List<Journal>());
        _mockUnitOfWork.Setup(u => u.ApiDataSources.Update(It.IsAny<ApiDataSource>()));
        _mockSyncProposalRepo.Setup(r => r.AddAsync(It.IsAny<SyncProposal>()))
            .Callback<SyncProposal>(p => p.Id = 1)
            .Returns(Task.CompletedTask);

        _mockSemanticClient.Setup(c => c.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("API Down"));

        var result = await _syncService.RunSyncAsync("SemanticScholar");

        result.Results.Should().HaveCount(1);
        var syncResult = result.Results[0];
        syncResult.Status.Should().Be("Failed");
        syncResult.Query.Should().NotBeNullOrEmpty();
        syncResult.Message.Should().Contain("Sync failed");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunSyncAsync_WithMultipleQueries_ShouldCreateOneProposalPerQuery()
    {
        var source = new ApiDataSource { Id = 1, Name = "SemanticScholar", IsActive = true };
        _mockUnitOfWork.Setup(u => u.ApiDataSources.GetActiveAsync())
            .ReturnsAsync(new List<ApiDataSource> { source });
        _mockUnitOfWork.Setup(u => u.SyncLogs.AddAsync(It.IsAny<SyncLog>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.Journals.GetAllAsync())
            .ReturnsAsync(new List<Journal> { new() { Id = 1 } });
        _mockUnitOfWork.Setup(u => u.ApiDataSources.Update(It.IsAny<ApiDataSource>()));
        _mockSyncProposalRepo.Setup(r => r.AddAsync(It.IsAny<SyncProposal>()))
            .Callback<SyncProposal>(p => p.Id = Random.Shared.Next(100, 999))
            .Returns(Task.CompletedTask);
        _mockSyncProposalRepo.Setup(r => r.IsPaperAlreadyQueuedOrStoredAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockSemanticClient.Setup(c => c.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ExternalPaperDto>
            {
                new() { ExternalId = Guid.NewGuid().ToString(), Title = "Paper", Source = "SemanticScholar", Doi = "10.123/x" }
            });

        var queries = new List<string> { "machine learning", "deep learning", "quantum computing" };

        var result = await _syncService.RunSyncAsync("SemanticScholar", searchQueries: queries);

        // 1 source × 3 queries = 3 SyncResults, each backed by its own proposal.
        result.Results.Should().HaveCount(3);
        result.Results.Select(r => r.Query).Should().BeEquivalentTo(queries);
        result.Results.Should().OnlyContain(r => r.Status == "Completed");
        result.Results.Should().OnlyContain(r => r.SyncProposalId.HasValue);
        result.Results.Select(r => r.SyncProposalId!.Value).Distinct().Should().HaveCount(3,
            "each query must produce a distinct proposal");
        _mockNotificationService.Verify(
            n => n.NotifyAdminsPendingSyncAsync(It.IsAny<int>(), It.IsAny<int>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task RunSyncAsync_WithDuplicateQueries_ShouldDedupeThem()
    {
        var source = new ApiDataSource { Id = 1, Name = "SemanticScholar", IsActive = true };
        _mockUnitOfWork.Setup(u => u.ApiDataSources.GetActiveAsync())
            .ReturnsAsync(new List<ApiDataSource> { source });
        _mockUnitOfWork.Setup(u => u.SyncLogs.AddAsync(It.IsAny<SyncLog>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.Journals.GetAllAsync())
            .ReturnsAsync(new List<Journal> { new() { Id = 1 } });
        _mockUnitOfWork.Setup(u => u.ApiDataSources.Update(It.IsAny<ApiDataSource>()));
        _mockSyncProposalRepo.Setup(r => r.AddAsync(It.IsAny<SyncProposal>()))
            .Callback<SyncProposal>(p => p.Id = Random.Shared.Next(100, 999))
            .Returns(Task.CompletedTask);
        _mockSyncProposalRepo.Setup(r => r.IsPaperAlreadyQueuedOrStoredAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockSemanticClient.Setup(c => c.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<ExternalPaperDto>());

        // Caller passes the same query multiple times with different casing / whitespace.
        var queries = new List<string> { "  AI  ", "ai", "AI", "robotics" };

        var result = await _syncService.RunSyncAsync("SemanticScholar", searchQueries: queries);

        // "  AI  ", "ai", "AI" dedupe to 1 (case-insensitive after trim); "robotics" is distinct → 2 results total.
        result.Results.Should().HaveCount(2);
        result.Results.Select(r => r.Query!.Trim().ToLowerInvariant())
            .Should().BeEquivalentTo(new[] { "ai", "robotics" });
    }
}
