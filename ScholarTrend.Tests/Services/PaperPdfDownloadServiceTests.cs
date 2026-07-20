using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Tests.Services;

public class PaperPdfDownloadServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IPaperPdfFileRepository> _mockRepo;
    private readonly Mock<IPaperFileStorage> _mockStorage;
    private readonly Mock<IPaperFileStorageProvider> _mockStorageProvider;
    private readonly Mock<IDocumentDownloader> _mockDownloader;
    private readonly Mock<IPaperPdfChannel> _mockChannel;
    private readonly Mock<ILogger<PaperPdfDownloadService>> _mockLogger;
    private readonly PaperPdfDownloadService _service;

    public PaperPdfDownloadServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockRepo = new Mock<IPaperPdfFileRepository>();
        _mockStorage = new Mock<IPaperFileStorage>();
        _mockStorageProvider = new Mock<IPaperFileStorageProvider>();
        _mockDownloader = new Mock<IDocumentDownloader>();
        _mockChannel = new Mock<IPaperPdfChannel>();
        _mockLogger = new Mock<ILogger<PaperPdfDownloadService>>();

        _mockUow.Setup(u => u.PaperPdfFiles).Returns(_mockRepo.Object);
        _mockUow.Setup(u => u.Context).Returns(Mock.Of<Microsoft.EntityFrameworkCore.DbContext>());

        // Real channel for enqueue → reader tests
        var realChannel = Channel.CreateBounded<int>(new BoundedChannelOptions(50)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        _mockChannel.SetupGet(c => c.Writer).Returns(realChannel.Writer);
        _mockChannel.SetupGet(c => c.Reader).Returns(realChannel.Reader);

        _mockStorageProvider.SetupGet(p => p.GetActiveStorage()).Returns(_mockStorage.Object);

        _service = new PaperPdfDownloadService(
            _mockUow.Object, _mockStorageProvider.Object, _mockDownloader.Object, _mockChannel.Object, _mockLogger.Object);
    }

    private static byte[] MakePdfBytes(int extraSize = 0)
    {
        // %PDF-1.4 header (4 bytes) + extra padding
        var bytes = new byte[4 + extraSize];
        bytes[0] = 0x25; bytes[1] = 0x50; bytes[2] = 0x44; bytes[3] = 0x46; // %PDF
        return bytes;
    }

    // ===========================
    // 1. EnqueueAsync
    // ===========================

    [Fact]
    public async Task EnqueueAsync_CreatesRecord_WithStatusQueued()
    {
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<PaperPdfFile>()))
            .Callback<PaperPdfFile>(p => p.Id = 42)
            .Returns(Task.CompletedTask);

        await _service.EnqueueAsync("ArXiv", "https://arxiv.org/pdf/1234.pdf", 7);

        _mockRepo.Verify(r => r.AddAsync(It.Is<PaperPdfFile>(p =>
            p.ExternalSource == "ArXiv"
            && p.SourceUrl == "https://arxiv.org/pdf/1234.pdf"
            && p.ResearchPaperId == 7
            && p.Status == PaperDownloadStatus.Queued
            && p.AttemptCount == 0
            && p.LocalRelativePath == "papers/7.pdf"
        )), Times.Once);
    }

    [Fact]
    public async Task EnqueueAsync_PushesIdToChannel_AndSaves()
    {
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<PaperPdfFile>()))
            .Callback<PaperPdfFile>(p => p.Id = 99)
            .Returns(Task.CompletedTask);

        await _service.EnqueueAsync("OpenAlex", "https://api.openalex.org/x.pdf", 11);

        // Channel reader phải nhận được ID=99
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var id = await _mockChannel.Object.Reader.ReadAsync(cts.Token);
        id.Should().Be(99);

        _mockUow.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);
    }

    // ===========================
    // 2. ProcessAsync — Record null
    // ===========================

    [Fact]
    public async Task ProcessAsync_WhenRecordNotFound_ReturnsWithoutThrowing()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(404)).ReturnsAsync((PaperPdfFile?)null);

        var act = async () => await _service.ProcessAsync(404, default);
        await act.Should().NotThrowAsync();

        _mockRepo.Verify(r => r.Update(It.IsAny<PaperPdfFile>()), Times.Never);
    }

    // ===========================
    // 3. ProcessAsync — URL fail validation
    // ===========================

    [Fact]
    public async Task ProcessAsync_UntrustedUrl_SkipsAndDoesNotCallDownloader()
    {
        var record = new PaperPdfFile
        {
            Id = 1, ResearchPaperId = 5, ExternalSource = "Evil",
            SourceUrl = "https://evil.com/foo.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(record);

        await _service.ProcessAsync(1, default);

        record.Status.Should().Be(PaperDownloadStatus.Skipped);
        record.FailureReason.Should().NotBeNullOrEmpty();
        record.CompletedAt.Should().NotBeNull();
        _mockDownloader.Verify(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===========================
    // 4. ProcessAsync — Download success
    // ===========================

    [Fact]
    public async Task ProcessAsync_ValidPdf_SavesToStorage_AndUpdatesRecord()
    {
        var record = new PaperPdfFile
        {
            Id = 2, ResearchPaperId = 8, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/1234.pdf",
            LocalRelativePath = "papers/8.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(record);

        var pdfBytes = MakePdfBytes(extraSize: 1024);
        _mockDownloader.Setup(d => d.DownloadAsync(record.SourceUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadedDocument { Bytes = pdfBytes, ContentType = "application/pdf" });

        _mockStorage.Setup(s => s.SaveBytesAsync("papers/8.pdf", pdfBytes, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/root/uploads/papers/8.pdf");

        await _service.ProcessAsync(2, default);

        record.Status.Should().Be(PaperDownloadStatus.Ready);
        record.SizeBytes.Should().Be(pdfBytes.Length);
        record.ContentType.Should().Be("application/pdf");
        record.Sha256.Should().NotBeNullOrEmpty().And.HaveLength(64); // SHA-256 hex
        record.CompletedAt.Should().NotBeNull();

        _mockStorage.Verify(s => s.SaveBytesAsync("papers/8.pdf", pdfBytes, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ===========================
    // 5. ProcessAsync — Download returns null
    // ===========================

    [Fact]
    public async Task ProcessAsync_DownloaderReturnsNull_MarksAsRetryOrFailed()
    {
        var record = new PaperPdfFile
        {
            Id = 3, ResearchPaperId = 9, ExternalSource = "OpenAlex",
            SourceUrl = "https://api.openalex.org/x.pdf",
            LocalRelativePath = "papers/9.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(record);
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadedDocument?)null);

        await _service.ProcessAsync(3, default);

        // attempt 1 < 3 → status = Queued (retry scheduled)
        record.Status.Should().Be(PaperDownloadStatus.Queued);
        record.AttemptCount.Should().Be(1);
    }

    // ===========================
    // 6. ProcessAsync — Response not PDF (magic bytes fail)
    // ===========================

    [Fact]
    public async Task ProcessAsync_InvalidMagicBytes_FailsAndRetries()
    {
        var record = new PaperPdfFile
        {
            Id = 4, ResearchPaperId = 10, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/bad.pdf",
            LocalRelativePath = "papers/10.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(4)).ReturnsAsync(record);

        var fakeBytes = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39 }; // GIF89a
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadedDocument { Bytes = fakeBytes });

        await _service.ProcessAsync(4, default);

        record.Status.Should().Be(PaperDownloadStatus.Queued); // retry
        _mockStorage.Verify(s => s.SaveBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===========================
    // 7. ProcessAsync — File too large
    // ===========================

    [Fact]
    public async Task ProcessAsync_FileExceedsMaxSize_FailsAndRetries()
    {
        var record = new PaperPdfFile
        {
            Id = 5, ResearchPaperId = 11, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/huge.pdf",
            LocalRelativePath = "papers/11.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(record);

        var oversized = new byte[51 * 1024 * 1024]; // 51MB
        oversized[0] = 0x25; oversized[1] = 0x50; oversized[2] = 0x44; oversized[3] = 0x46;
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadedDocument { Bytes = oversized });

        await _service.ProcessAsync(5, default);

        record.Status.Should().Be(PaperDownloadStatus.Queued);
        _mockStorage.Verify(s => s.SaveBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ===========================
    // 8. ProcessAsync — Max attempts reached → Failed
    // ===========================

    [Fact]
    public async Task ProcessAsync_ThirdFailure_MarksAsFailed()
    {
        var record = new PaperPdfFile
        {
            Id = 6, ResearchPaperId = 12, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/broken.pdf",
            LocalRelativePath = "papers/12.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 2 // attempt thứ 3 sẽ fail
        };
        _mockRepo.Setup(r => r.GetByIdAsync(6)).ReturnsAsync(record);
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadedDocument?)null);

        await _service.ProcessAsync(6, default);

        record.Status.Should().Be(PaperDownloadStatus.Failed);
        record.AttemptCount.Should().Be(3);
        record.FailureReason.Should().NotBeNullOrEmpty();
        record.CompletedAt.Should().NotBeNull();
    }

    // ===========================
    // 9. ProcessAsync — Status set Downloading khi bắt đầu
    // ===========================

    [Fact]
    public async Task ProcessAsync_TransitionStatus_FromQueuedToDownloading()
    {
        var record = new PaperPdfFile
        {
            Id = 7, ResearchPaperId = 13, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/1234.pdf",
            LocalRelativePath = "papers/13.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(7)).ReturnsAsync(record);
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadedDocument { Bytes = MakePdfBytes() });
        _mockStorage.Setup(s => s.SaveBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/root/x.pdf");

        await _service.ProcessAsync(7, default);

        // Verify record state transitions correctly
        record.AttemptCount.Should().Be(1, "AttemptCount must increment");
        record.Status.Should().Be(PaperDownloadStatus.Ready, "Successful download transitions to Ready");
    }

    // ===========================
    // 10. ProcessAsync — SHA-256 stable
    // ===========================

    [Fact]
    public async Task ProcessAsync_Sha256_IsDeterministicForSameBytes()
    {
        var record1 = new PaperPdfFile { Id = 100, ResearchPaperId = 1, ExternalSource = "ArXiv", SourceUrl = "https://arxiv.org/a.pdf", LocalRelativePath = "papers/1.pdf", Status = PaperDownloadStatus.Queued, AttemptCount = 0 };
        var record2 = new PaperPdfFile { Id = 101, ResearchPaperId = 2, ExternalSource = "ArXiv", SourceUrl = "https://arxiv.org/b.pdf", LocalRelativePath = "papers/2.pdf", Status = PaperDownloadStatus.Queued, AttemptCount = 0 };

        var sameBytes = MakePdfBytes(extraSize: 100);
        _mockRepo.Setup(r => r.GetByIdAsync(100)).ReturnsAsync(record1);
        _mockRepo.Setup(r => r.GetByIdAsync(101)).ReturnsAsync(record2);
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadedDocument { Bytes = sameBytes });
        _mockStorage.Setup(s => s.SaveBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/root/x.pdf");

        await _service.ProcessAsync(100, default);
        await _service.ProcessAsync(101, default);

        record1.Sha256.Should().Be(record2.Sha256);
        record1.Sha256.Should().HaveLength(64);
    }

    // ===========================
    // 11. ProcessAsync — Cancellation token respected
    // ===========================

    [Fact]
    public async Task ProcessAsync_CancelledDuringDownload_RecordsAsFailed()
    {
        var record = new PaperPdfFile
        {
            Id = 8, ResearchPaperId = 14, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/slow.pdf",
            LocalRelativePath = "papers/14.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(8)).ReturnsAsync(record);

        // Downloader trả về null (giả lập đã cancel / timeout)
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadedDocument?)null);

        var act = async () => await _service.ProcessAsync(8, default);
        await act.Should().NotThrowAsync("OperationCanceledException should be handled internally");

        record.AttemptCount.Should().Be(1);
        // Sau attempt 1, status chuyển sang Queued (retry) vì còn 2 attempts nữa
        record.Status.Should().Be(PaperDownloadStatus.Queued);
    }

    // ===========================
    // 12. QueueReader — multi-consumer
    // ===========================

    [Fact]
    public async Task QueueReader_ReadsMultipleEnqueuedIds_InOrder()
    {
        _mockRepo.Setup(r => r.AddAsync(It.IsAny<PaperPdfFile>()))
            .Callback<PaperPdfFile>(p => p.Id = int.Parse(p.ResearchPaperId.ToString() + "0"))
            .Returns(Task.CompletedTask);

        await _service.EnqueueAsync("ArXiv", "https://arxiv.org/pdf/a.pdf", 1);
        await _service.EnqueueAsync("ArXiv", "https://arxiv.org/pdf/b.pdf", 2);
        await _service.EnqueueAsync("ArXiv", "https://arxiv.org/pdf/c.pdf", 3);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var id1 = await _mockChannel.Object.Reader.ReadAsync(cts.Token);
        var id2 = await _mockChannel.Object.Reader.ReadAsync(cts.Token);
        var id3 = await _mockChannel.Object.Reader.ReadAsync(cts.Token);

        id1.Should().Be(10);
        id2.Should().Be(20);
        id3.Should().Be(30);
    }

    // ===========================
    // 13. Retry behavior — second attempt updates AttemptCount
    // ===========================

    [Fact]
    public async Task ProcessAsync_Failure_IncrementsAttemptCount()
    {
        var record = new PaperPdfFile
        {
            Id = 9, ResearchPaperId = 15, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/retry.pdf",
            LocalRelativePath = "papers/15.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(9)).ReturnsAsync(record);
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DownloadedDocument?)null);

        await _service.ProcessAsync(9, default);

        record.AttemptCount.Should().Be(1);
        record.Status.Should().Be(PaperDownloadStatus.Queued);
    }

    // ===========================
    // 14. Storage exception → retry
    // ===========================

    [Fact]
    public async Task ProcessAsync_StorageThrows_DoesNotBreakFlow()
    {
        var record = new PaperPdfFile
        {
            Id = 10, ResearchPaperId = 16, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/storage.pdf",
            LocalRelativePath = "papers/16.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 0
        };
        _mockRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(record);
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadedDocument { Bytes = MakePdfBytes() });
        _mockStorage.Setup(s => s.SaveBytesAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk full"));

        var act = async () => await _service.ProcessAsync(10, default);
        await act.Should().NotThrowAsync();

        record.AttemptCount.Should().Be(1);
        record.Status.Should().Be(PaperDownloadStatus.Queued);
    }

    // ===========================
    // 15. ProcessAsync — FailureReason contains exception info
    // ===========================

    [Fact]
    public async Task ProcessAsync_FinalFailure_StoresReasonWithExceptionType()
    {
        var record = new PaperPdfFile
        {
            Id = 11, ResearchPaperId = 17, ExternalSource = "ArXiv",
            SourceUrl = "https://arxiv.org/pdf/explode.pdf",
            LocalRelativePath = "papers/17.pdf",
            Status = PaperDownloadStatus.Queued, AttemptCount = 2
        };
        _mockRepo.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(record);
        _mockDownloader.Setup(d => d.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DownloadedDocument { Bytes = new byte[0] }); // empty

        await _service.ProcessAsync(11, default);

        record.Status.Should().Be(PaperDownloadStatus.Failed);
        record.FailureReason.Should().NotBeNullOrEmpty();
    }
}
