using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Application.Interfaces.Repositories;

namespace ScholarTrend.Tests.Services;

public class SyncServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IPaperImportRepository> _mockPaperImportRepo;
    private readonly Mock<ISemanticScholarClient> _mockSemanticClient;
    private readonly Mock<IOpenAlexClient> _mockOpenAlexClient;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly SyncService _syncService;

    public SyncServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockPaperImportRepo = new Mock<IPaperImportRepository>();
        _mockSemanticClient = new Mock<ISemanticScholarClient>();
        _mockOpenAlexClient = new Mock<IOpenAlexClient>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockConfig = new Mock<IConfiguration>();

        _syncService = new SyncService(
            _mockUnitOfWork.Object,
            _mockPaperImportRepo.Object,
            _mockSemanticClient.Object,
            _mockOpenAlexClient.Object,
            _mockNotificationService.Object,
            _mockConfig.Object
        );
    }

    [Fact]
    public async Task RunSyncAsync_ShouldSyncSemanticScholar_WhenCalledWithSource()
    {
        // Arrange
        var source = new ApiDataSource { Name = "SemanticScholar", IsActive = true };
        _mockUnitOfWork.Setup(u => u.ApiDataSources.GetActiveAsync())
            .ReturnsAsync(new List<ApiDataSource> { source });
        _mockUnitOfWork.Setup(u => u.SyncLogs.AddAsync(It.IsAny<SyncLog>()))
            .Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.Journals.GetAllAsync())
            .ReturnsAsync(new List<Journal> { new() { Id = 1 } });

        var externalPapers = new List<ExternalPaperDto>
        {
            new() { ExternalId = "ext1", Title = "Paper 1", Doi = "10.123/1" }
        };

        _mockSemanticClient.Setup(c => c.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(externalPapers);

        _mockPaperImportRepo.Setup(r => r.ImportAsync(It.IsAny<ExternalPaperDto>(), It.IsAny<int?>()))
        .ReturnsAsync(new ResearchPaperImportResult 
        { 
            IsNew = true, 
            PaperId = 101 
        });

        // Act
        var result = await _syncService.RunSyncAsync("SemanticScholar");

        // Assert
        result.Status.Should().Be("Completed");
        result.PapersAdded.Should().Be(1);
        _mockSemanticClient.Verify(c => c.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
        _mockNotificationService.Verify(n => n.NotifyFollowersForNewPaperAsync(101), Times.Once);
    }

    [Fact]
    public async Task RunSyncAsync_ShouldHandleApiFailure_Gracefully()
    {
        // Arrange
        var source = new ApiDataSource { Name = "SemanticScholar", IsActive = true };
        _mockUnitOfWork.Setup(u => u.ApiDataSources.GetActiveAsync())
            .ReturnsAsync(new List<ApiDataSource> { source });
        _mockUnitOfWork.Setup(u => u.SyncLogs.AddAsync(It.IsAny<SyncLog>()))
            .Returns(Task.CompletedTask);

        _mockSemanticClient.Setup(c => c.SearchPapersAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("API Down"));

        // Act
        var result = await _syncService.RunSyncAsync("SemanticScholar");

        // Assert
        result.Status.Should().Be("Failed");
        result.Message.Should().Be("API Down");
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }
}
