using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ScholarTrend.Infrastructure.Storage;

namespace ScholarTrend.Tests.Storage;

public class LocalPaperFileStorageTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly LocalPaperFileStorage _storage;
    private readonly StorageSettings _settings;

    public LocalPaperFileStorageTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "scholartrend-test-" + Guid.NewGuid().ToString("N"));
        _settings = new StorageSettings { StoragePath = _tempRoot };
        _storage = new LocalPaperFileStorage(_settings, Mock.Of<ILogger<LocalPaperFileStorage>>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Constructor_CreatesPapersDirectory()
    {
        var papersDir = Path.Combine(_tempRoot, "papers");
        Directory.Exists(papersDir).Should().BeTrue();
    }

    [Fact]
    public void Constructor_UsesDefault_WhenStoragePathEmpty()
    {
        var emptySettings = new StorageSettings { StoragePath = "" };
        var action = () => new LocalPaperFileStorage(emptySettings, Mock.Of<ILogger<LocalPaperFileStorage>>());
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_HandlesAbsolutePath()
    {
        var absPath = Path.Combine(Path.GetTempPath(), "abs-path-" + Guid.NewGuid().ToString("N"));
        var settings = new StorageSettings { StoragePath = absPath };
        var action = () => new LocalPaperFileStorage(settings, Mock.Of<ILogger<LocalPaperFileStorage>>());
        action.Should().NotThrow();
        try
        {
            if (Directory.Exists(absPath)) Directory.Delete(absPath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; không fail test nếu OS giữ file handle
        }
    }

    [Fact]
    public async Task SaveBytesAsync_WritesFile_AndReturnsAbsolutePath()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var result = await _storage.SaveBytesAsync("papers/10.pdf", bytes, default);

        result.Should().NotBeNullOrEmpty();
        File.Exists(result).Should().BeTrue();
        File.ReadAllBytes(result).Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task SaveBytesAsync_OverwritesExistingFile()
    {
        var rel = "papers/over.pdf";
        await _storage.SaveBytesAsync(rel, new byte[] { 1, 2, 3 }, default);
        await _storage.SaveBytesAsync(rel, new byte[] { 9, 9, 9 }, default);

        var abs = _storage.ResolveAbsolutePath(rel);
        File.ReadAllBytes(abs).Should().BeEquivalentTo(new byte[] { 9, 9, 9 });
    }

    [Fact]
    public async Task SaveBytesAsync_CreatesSubdirectories()
    {
        var rel = "papers/2024/group/subgroup/100.pdf";
        await _storage.SaveBytesAsync(rel, new byte[] { 0xAA }, default);

        var abs = _storage.ResolveAbsolutePath(rel);
        File.Exists(abs).Should().BeTrue();
    }

    [Fact]
    public async Task SaveBytesAsync_AcceptsEmptyBytes()
    {
        var rel = "papers/empty.pdf";
        await _storage.SaveBytesAsync(rel, Array.Empty<byte>(), default);

        var abs = _storage.ResolveAbsolutePath(rel);
        File.Exists(abs).Should().BeTrue();
        File.ReadAllBytes(abs).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveBytesAsync_HandlesLargeFile()
    {
        var bytes = new byte[5 * 1024 * 1024]; // 5 MB
        new Random(42).NextBytes(bytes);

        var rel = "papers/large.pdf";
        await _storage.SaveBytesAsync(rel, bytes, default);

        var abs = _storage.ResolveAbsolutePath(rel);
        var read = File.ReadAllBytes(abs);
        read.Length.Should().Be(bytes.Length);
        read.Should().BeEquivalentTo(bytes);
    }

    [Theory]
    [InlineData("papers/1.pdf", "1.pdf")]
    [InlineData("/papers/1.pdf", "1.pdf")]                // leading slash
    [InlineData("papers\\1.pdf", "1.pdf")]                 // backslash
    [InlineData("papers/sub/2.pdf", "2.pdf")]              // nested
    [InlineData("papers/very/deep/path/3.pdf", "3.pdf")]
    public void ResolveAbsolutePath_NormalizesPath(string input, string expectedLeaf)
    {
        var abs = _storage.ResolveAbsolutePath(input);
        abs.Should().EndWith(expectedLeaf);
        abs.Should().StartWith(_tempRoot);
    }

    [Fact]
    public void DeleteIfExists_RemovesFile_WhenExists()
    {
        var rel = "papers/delete-me.pdf";
        File.WriteAllBytes(_storage.ResolveAbsolutePath(rel), new byte[] { 1 });
        File.Exists(_storage.ResolveAbsolutePath(rel)).Should().BeTrue();

        _storage.DeleteIfExists(rel);

        File.Exists(_storage.ResolveAbsolutePath(rel)).Should().BeFalse();
    }

    [Fact]
    public void DeleteIfExists_DoesNotThrow_WhenFileNotFound()
    {
        var act = () => _storage.DeleteIfExists("papers/never-existed.pdf");
        act.Should().NotThrow();
    }

    [Fact]
    public void DeleteIfExists_DoesNotThrow_WhenParentDirNotFound()
    {
        var act = () => _storage.DeleteIfExists("papers/never/created/path.pdf");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SaveBytesAsync_AfterSave_FileIsReadable()
    {
        // Mô phỏng PDF thật
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };  // %PDF-
        var rel = "papers/real.pdf";
        await _storage.SaveBytesAsync(rel, pdfBytes, default);

        var read = await File.ReadAllBytesAsync(_storage.ResolveAbsolutePath(rel));
        read.Should().BeEquivalentTo(pdfBytes);
    }
}