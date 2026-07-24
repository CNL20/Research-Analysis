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

namespace ScholarTrend.Tests.Services;

public class TopicsJsonSyncServiceTests
{
    [Fact]
    public async Task ApprovePendingSyncAsync_PassesTopicsAndJournal_ToImport()
    {
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockSyncProposalRepo = new Mock<ISyncProposalRepository>();
        var mockPaperImportRepo = new Mock<IPaperImportRepository>();
        var mockTrendAggregation = new Mock<ITrendAggregationService>();
        var mockConfig = new Mock<IConfiguration>();
        ExternalPaperDto? imported = null;

        mockConfig.Setup(c => c["ExternalApis:SemanticScholar:SearchQuery"]).Returns("ai");
        mockConfig.Setup(c => c.GetSection("SyncSchedule:SearchQueries"))
            .Returns(Mock.Of<IConfigurationSection>(s => s.GetChildren() == Enumerable.Empty<IConfigurationSection>()));

        mockUnitOfWork.Setup(u => u.SyncProposals).Returns(mockSyncProposalRepo.Object);
        mockPaperImportRepo
            .Setup(r => r.ImportAsync(It.IsAny<ExternalPaperDto>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalPaperDto, int?, CancellationToken>((dto, _, _) => imported = dto)
            .ReturnsAsync(new ResearchPaperImportResult { IsNew = true, PaperId = 99 });

        var proposal = new SyncProposal
        {
            Id = 7,
            Status = SyncProposalStatus.Pending,
            PendingPapers =
            [
                new PendingPaper
                {
                    Id = 1,
                    ExternalId = "ext-1",
                    ExternalSource = "OpenAlex",
                    Title = "Transformer paper",
                    AuthorNamesJson = "[\"Alice\"]",
                    KeywordsJson = "[\"transformer\",\"nlp\"]",
                    TopicsJson = "[\"Natural Language Processing\",\"Machine Learning\"]",
                    JournalName = "Journal of AI Research",
                    Status = PendingPaperStatus.Pending
                }
            ]
        };

        mockSyncProposalRepo.Setup(r => r.GetByIdWithPapersAsync(7)).ReturnsAsync(proposal);
        mockUnitOfWork.Setup(u => u.Journals.GetAllAsync())
            .ReturnsAsync([new Journal { Id = 1, Name = "Default" }]);

        var service = new SyncService(
            mockUnitOfWork.Object,
            mockPaperImportRepo.Object,
            Mock.Of<ISemanticScholarClient>(),
            Mock.Of<IOpenAlexClient>(),
            Mock.Of<ICrossrefClient>(),
            Mock.Of<IArXivClient>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IPaperPdfEnqueuer>(),
            mockTrendAggregation.Object,
            Mock.Of<IBackgroundJobClient>(),
            mockConfig.Object,
            Mock.Of<ILogger<SyncService>>());

        await service.ApprovePendingSyncAsync(7, "admin", new ApproveSyncRequest());

        imported.Should().NotBeNull();
        imported!.Topics.Should().BeEquivalentTo(["Natural Language Processing", "Machine Learning"]);
        imported.Keywords.Should().BeEquivalentTo(["transformer", "nlp"]);
        imported.Journal.Should().Be("Journal of AI Research");
        mockTrendAggregation.Verify(t => t.ScheduleRebuild(), Times.Once);
    }
}
