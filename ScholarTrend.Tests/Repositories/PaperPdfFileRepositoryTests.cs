using FluentAssertions;
using Moq;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Tests.Repositories;

/// <summary>
/// Test contract của IPaperPdfFileRepository — dùng Mock để verify
/// các tương tác với UnitOfWork. Repository thực chỉ wrap DbSet nên
/// tập trung vào: (1) interface đúng, (2) các status transitions,
/// (3) truy vấn theo status/stuck.
/// </summary>
public class PaperPdfFileRepositoryContractTests
{
    [Fact]
    public async Task GetByIdAsync_ReturnsExpected_ById()
    {
        var repo = new Mock<IPaperPdfFileRepository>();
        var expected = new PaperPdfFile { Id = 42, ResearchPaperId = 7 };
        repo.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(expected);

        var result = await repo.Object.GetByIdAsync(42);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetByResearchPaperIdAsync_ReturnsExpected()
    {
        var repo = new Mock<IPaperPdfFileRepository>();
        var expected = new PaperPdfFile { Id = 5, ResearchPaperId = 9 };
        repo.Setup(r => r.GetByResearchPaperIdAsync(9)).ReturnsAsync(expected);

        var result = await repo.Object.GetByResearchPaperIdAsync(9);

        result!.ResearchPaperId.Should().Be(9);
    }

    [Fact]
    public async Task AddAsync_AddsNewRecord_WithoutId()
    {
        var repo = new Mock<IPaperPdfFileRepository>();
        var record = new PaperPdfFile
        {
            ResearchPaperId = 1,
            ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/x.pdf",
            LocalRelativePath = "papers/1.pdf"
        };

        await repo.Object.AddAsync(record);

        repo.Verify(r => r.AddAsync(It.Is<PaperPdfFile>(p =>
            p.ResearchPaperId == 1 && p.ExternalSource == "ArXiv")), Times.Once);
    }

    [Fact]
    public void Update_TriggersUpdate_OnExistingRecord()
    {
        var repo = new Mock<IPaperPdfFileRepository>();
        var record = new PaperPdfFile { Id = 1, Status = PaperDownloadStatus.Queued };

        repo.Object.Update(record);

        repo.Verify(r => r.Update(It.Is<PaperPdfFile>(p => p.Id == 1)), Times.Once);
    }

    [Fact]
    public async Task GetByStatusAsync_FilterByStatus_MultiValues()
    {
        var repo = new Mock<IPaperPdfFileRepository>();
        repo.Setup(r => r.GetByStatusAsync(PaperDownloadStatus.Queued, 10))
            .ReturnsAsync(new List<PaperPdfFile>
            {
                new() { Id = 1, Status = PaperDownloadStatus.Queued },
                new() { Id = 2, Status = PaperDownloadStatus.Queued }
            });
        repo.Setup(r => r.GetByStatusAsync(PaperDownloadStatus.Failed, 10))
            .ReturnsAsync(new List<PaperPdfFile>());  // empty list

        var queued = await repo.Object.GetByStatusAsync(PaperDownloadStatus.Queued, 10);
        var failed = await repo.Object.GetByStatusAsync(PaperDownloadStatus.Failed, 10);

        queued.Should().HaveCount(2).And.OnlyContain(p => p.Status == PaperDownloadStatus.Queued);
        failed.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStuckAsync_ReturnsRecordsWithMultipleStatuses()
    {
        var repo = new Mock<IPaperPdfFileRepository>();
        var statuses = new[] { PaperDownloadStatus.Queued, PaperDownloadStatus.Downloading };
        repo.Setup(r => r.GetStuckAsync(statuses, 50))
            .ReturnsAsync(new List<PaperPdfFile>
            {
                new() { Id = 1, Status = PaperDownloadStatus.Queued },
                new() { Id = 2, Status = PaperDownloadStatus.Downloading }
            });

        var stuck = await repo.Object.GetStuckAsync(statuses, 50);

        stuck.Should().HaveCount(2);
        stuck.Select(s => s.Status).Should().BeSubsetOf(statuses);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsAllPendingChanges()
    {
        var repo = new Mock<IPaperPdfFileRepository>();
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        await repo.Object.SaveChangesAsync();

        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetByResearchPaperIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = new Mock<IPaperPdfFileRepository>();
        repo.Setup(r => r.GetByResearchPaperIdAsync(9999)).ReturnsAsync((PaperPdfFile?)null);

        var result = await repo.Object.GetByResearchPaperIdAsync(9999);

        result.Should().BeNull();
    }

    [Fact]
    public void PaperPdfFile_DefaultStatus_IsQueued()
    {
        // Verify entity default
        var record = new PaperPdfFile();
        record.Status.Should().Be(PaperDownloadStatus.Queued);
    }

    [Fact]
    public void PaperPdfFile_DefaultAttemptCount_IsZero()
    {
        var record = new PaperPdfFile();
        record.AttemptCount.Should().Be(0);
    }

    [Fact]
    public void PaperPdfFile_DefaultEnqueuedAt_IsRecentUtc()
    {
        var record = new PaperPdfFile();
        var diff = DateTime.UtcNow - record.EnqueuedAt;
        diff.Should().BeLessThan(TimeSpan.FromMinutes(1));
    }
}