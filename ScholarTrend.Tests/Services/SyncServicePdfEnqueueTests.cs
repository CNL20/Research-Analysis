using FluentAssertions;
using Hangfire;
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
using System.Threading.Channels;

namespace ScholarTrend.Tests.Services;

/// <summary>
/// Tests cho SyncService.TryEnqueuePdfDownloadAsync (private method)
/// gián tiếp qua ApprovePendingSyncAsync flow.
/// Verify PDF chỉ được enqueue khi PdfAccessType = ArXiv hoặc OpenAccess.
/// </summary>
public class SyncServicePdfEnqueueTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ISyncProposalRepository> _mockSyncProposalRepo;
    private readonly Mock<IPaperImportRepository> _mockPaperImportRepo;
    private readonly Mock<ISemanticScholarClient> _mockSemanticClient;
    private readonly Mock<IOpenAlexClient> _mockOpenAlexClient;
    private readonly Mock<ICrossrefClient> _mockCrossrefClient;
    private readonly Mock<IArXivClient> _mockArXivClient;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IPaperPdfEnqueuer> _mockPaperPdfEnqueuer;
    private readonly Mock<ITrendAggregationService> _mockTrendAggregation;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<SyncService>> _mockLogger;
    private readonly SyncService _syncService;

    public SyncServicePdfEnqueueTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockSyncProposalRepo = new Mock<ISyncProposalRepository>();
        _mockPaperImportRepo = new Mock<IPaperImportRepository>();
        _mockSemanticClient = new Mock<ISemanticScholarClient>();
        _mockOpenAlexClient = new Mock<IOpenAlexClient>();
        _mockCrossrefClient = new Mock<ICrossrefClient>();
        _mockArXivClient = new Mock<IArXivClient>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockPaperPdfEnqueuer = new Mock<IPaperPdfEnqueuer>();
        _mockTrendAggregation = new Mock<ITrendAggregationService>();
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<SyncService>>();

        _mockConfig.Setup(c => c["ExternalApis:SemanticScholar:SearchQuery"]).Returns("ai");
        _mockConfig.Setup(c => c.GetSection("SyncSchedule:SearchQueries"))
            .Returns(Mock.Of<IConfigurationSection>(s => s.GetChildren() == Enumerable.Empty<IConfigurationSection>()));

        _mockUnitOfWork.Setup(u => u.SyncProposals).Returns(_mockSyncProposalRepo.Object);
        _mockPaperPdfEnqueuer.Setup(s => s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUnitOfWork.Setup(u => u.Journals.GetAllAsync())
            .ReturnsAsync(new List<Journal> { new() { Id = 1 } });

        _syncService = new SyncService(
            _mockUnitOfWork.Object, _mockPaperImportRepo.Object,
            _mockSemanticClient.Object, _mockOpenAlexClient.Object,
            _mockCrossrefClient.Object, _mockArXivClient.Object,
            _mockNotificationService.Object, _mockPaperPdfEnqueuer.Object,
            _mockTrendAggregation.Object,
            Mock.Of<IBackgroundJobClient>(),
            _mockConfig.Object,
            _mockLogger.Object);
    }

    private static PendingPaper MakePending(string accessType, string? pdfUrl, int id = 1, string source = "ArXiv")
        => new()
        {
            Id = id,
            ExternalId = "ext-" + id,
            ExternalSource = source,
            Title = "Paper " + id,
            AuthorNamesJson = "[]",
            Status = PendingPaperStatus.Pending,
            PdfUrl = pdfUrl,
            PdfAccessType = accessType
        };

    private SyncProposal MakeProposalWith(PendingPaper paper, int proposalId = 1)
        => new()
        {
            Id = proposalId,
            Status = SyncProposalStatus.Pending,
            PendingPapers = { paper }
        };

    private void SetupImport(int paperId) =>
        _mockPaperImportRepo.Setup(r => r.ImportAsync(It.IsAny<ExternalPaperDto>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResearchPaperImportResult { IsNew = true, PaperId = paperId });

    // ========================================================================
    // 4 trường hợp access type
    // ========================================================================

    [Fact]
    public async Task ApprovePendingSyncAsync_ArXivAccessType_EnqueuesPdfDownload()
    {
        var pending = MakePending(
            accessType: PaperDownloadStatus.AccessTypes.ArXiv,
            pdfUrl: "https://arxiv.org/pdf/1234.5678.pdf");
        var proposal = MakeProposalWith(pending);

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(1)).ReturnsAsync(proposal);
        SetupImport(paperId: 501);

        await _syncService.ApprovePendingSyncAsync(1, "admin-id", new ApproveSyncRequest());

        _mockPaperPdfEnqueuer.Verify(s =>
            s.EnqueueAsync("ArXiv", "https://arxiv.org/pdf/1234.5678.pdf", 501, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApprovePendingSyncAsync_OpenAccessType_EnqueuesPdfDownload()
    {
        var pending = MakePending(
            accessType: PaperDownloadStatus.AccessTypes.OpenAccess,
            pdfUrl: "https://api.openalex.org/W123/pdf",
            source: "OpenAlex");
        var proposal = MakeProposalWith(pending);

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(1)).ReturnsAsync(proposal);
        SetupImport(paperId: 502);

        await _syncService.ApprovePendingSyncAsync(1, "admin", new ApproveSyncRequest());

        _mockPaperPdfEnqueuer.Verify(s =>
            s.EnqueueAsync("OpenAlex", "https://api.openalex.org/W123/pdf", 502, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApprovePendingSyncAsync_PublisherAccessType_DoesNotEnqueue()
    {
        var pending = MakePending(
            accessType: PaperDownloadStatus.AccessTypes.Publisher,
            pdfUrl: "https://elsevier.com/foo.pdf",
            source: "Crossref");
        var proposal = MakeProposalWith(pending);

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(1)).ReturnsAsync(proposal);
        SetupImport(paperId: 503);

        await _syncService.ApprovePendingSyncAsync(1, "admin", new ApproveSyncRequest());

        _mockPaperPdfEnqueuer.Verify(s =>
            s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Publisher access type must NOT trigger PDF download");
    }

    [Fact]
    public async Task ApprovePendingSyncAsync_ClosedAccessType_DoesNotEnqueue()
    {
        var pending = MakePending(
            accessType: PaperDownloadStatus.AccessTypes.Closed,
            pdfUrl: "https://some-publisher.com/x.pdf",
            source: "OpenAlex");
        var proposal = MakeProposalWith(pending);

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(1)).ReturnsAsync(proposal);
        SetupImport(paperId: 504);

        await _syncService.ApprovePendingSyncAsync(1, "admin", new ApproveSyncRequest());

        _mockPaperPdfEnqueuer.Verify(s =>
            s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ========================================================================
    // PDF URL null/empty → KHÔNG enqueue bất kể access type
    // ========================================================================

    [Fact]
    public async Task ApprovePendingSyncAsync_NoPdfUrl_DoesNotEnqueue_EvenForArXiv()
    {
        var pending = MakePending(
            accessType: PaperDownloadStatus.AccessTypes.ArXiv,
            pdfUrl: null);
        var proposal = MakeProposalWith(pending);

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(1)).ReturnsAsync(proposal);
        SetupImport(paperId: 505);

        await _syncService.ApprovePendingSyncAsync(1, "admin", new ApproveSyncRequest());

        _mockPaperPdfEnqueuer.Verify(s =>
            s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApprovePendingSyncAsync_NullAccessType_DoesNotEnqueue()
    {
        var pending = MakePending(
            accessType: null,
            pdfUrl: "https://arxiv.org/pdf/5678.pdf",
            source: "ArXiv");
        var proposal = MakeProposalWith(pending);

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(1)).ReturnsAsync(proposal);
        SetupImport(paperId: 506);

        await _syncService.ApprovePendingSyncAsync(1, "admin", new ApproveSyncRequest());

        _mockPaperPdfEnqueuer.Verify(s =>
            s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Without explicit AccessType we must NOT enqueue (defensive: tránh tải nhầm)");
    }

    // ========================================================================
    // Multiple papers
    // ========================================================================

    [Fact]
    public async Task ApprovePendingSyncAsync_MultiplePapers_EnqueuesEachArXivPdf()
    {
        var pending1 = MakePending(accessType: PaperDownloadStatus.AccessTypes.ArXiv, pdfUrl: "https://arxiv.org/pdf/a.pdf", id: 1);
        var pending2 = MakePending(accessType: PaperDownloadStatus.AccessTypes.OpenAccess, pdfUrl: "https://api.openalex.org/b.pdf", id: 2, source: "OpenAlex");
        var pending3 = MakePending(accessType: PaperDownloadStatus.AccessTypes.Publisher, pdfUrl: "https://elsevier.com/c.pdf", id: 3, source: "Crossref");

        var proposal = new SyncProposal
        {
            Id = 1,
            Status = SyncProposalStatus.Pending,
            PendingPapers = { pending1, pending2, pending3 }
        };

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(1)).ReturnsAsync(proposal);
        SetupImport(paperId: 700); // Default cho cả 3

        await _syncService.ApprovePendingSyncAsync(1, "admin", new ApproveSyncRequest());

        // Verify chỉ enqueue cho 2 paper đầu
        _mockPaperPdfEnqueuer.Verify(s =>
            s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ========================================================================
    // Khi enqueue fail, KHÔNG làm fail cả approve flow
    // ========================================================================

    [Fact]
    public async Task ApprovePendingSyncAsync_PdfEnqueueThrows_StillApprovesSuccessfully()
    {
        var pending = MakePending(
            accessType: PaperDownloadStatus.AccessTypes.ArXiv,
            pdfUrl: "https://arxiv.org/pdf/throw.pdf");
        var proposal = MakeProposalWith(pending);

        _mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(1)).ReturnsAsync(proposal);
        SetupImport(paperId: 800);
        _mockPaperPdfEnqueuer.Setup(s =>
            s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue full"));

        var result = await _syncService.ApprovePendingSyncAsync(1, "admin", new ApproveSyncRequest());

        result.PapersApproved.Should().Be(1);
        result.Status.Should().NotBeNull();
    }
}
