using System.Text;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces;
using UglyToad.PdfPig;

namespace ScholarTrend.Infrastructure.Pdf;

/// <summary>
/// PdfPig-based PDF text extractor.
/// Hỗ trợ cả 2 input:
///   - Stream (B2 download về memory): ExtractTextAsync(Stream, ...).
///   - File path (Local disk): ExtractTextFromFileAsync(string path).
///
/// Lưu ý hiệu năng:
///   - Local: PdfPig có thể đọc random-access trên file → extract nhanh, không tốn memory.
///   - B2/Stream: PdfPig cần random-access → MemoryStream có thể đệm toàn bộ PDF (vài chục MB).
///     Với PDF > 50 MB, nên tải về local temp file rồi gọi ExtractTextFromFileAsync.
/// </summary>
public class PdfPigTextExtractor : IPaperTextExtractor
{
    private readonly ILogger<PdfPigTextExtractor> _logger;

    public PdfPigTextExtractor(ILogger<PdfPigTextExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<string?> ExtractTextAsync(Stream pdfStream, string sourceLabel, CancellationToken ct = default)
    {
        if (pdfStream == null)
        {
            _logger.LogWarning("PDF stream is null for {Source}", sourceLabel);
            return null;
        }

        // PdfPig cần stream seekable. B2 S3 SDK trả về HashStream (forward-only, không
        // hỗ trợ Position/Seek) → throw "HashStream does not support seeking".
        // Buffer về MemoryStream nếu cần. Với PDF > 50 MB sẽ tốn RAM nhưng đó là
        // trade-off chấp nhận được cho B2 (PDF research thường 1-10 MB).
        Stream workingStream = pdfStream;
        MemoryStream? buffered = null;

        try
        {
            if (!pdfStream.CanSeek)
            {
                _logger.LogDebug("Stream for {Source} is not seekable, buffering to MemoryStream", sourceLabel);
                buffered = new MemoryStream();
                await pdfStream.CopyToAsync(buffered, ct);
                buffered.Position = 0;
                workingStream = buffered;
            }

            // Magic-bytes check: %PDF-1.x
            var header = new byte[5];
            var read = await workingStream.ReadAsync(header.AsMemory(0, 5), ct);
            if (read < 5 || header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
            {
                _logger.LogWarning("Stream for {Source} is not a valid PDF (missing %PDF- header)", sourceLabel);
                return null;
            }
            workingStream.Position = 0; // reset

            try
            {
                return await Task.Run(() =>
                {
                    using var doc = PdfDocument.Open(workingStream);
                    var sb = new StringBuilder();
                    foreach (var page in doc.GetPages())
                    {
                        ct.ThrowIfCancellationRequested();
                        sb.AppendLine(page.Text);
                    }
                    return sb.ToString();
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract text from PDF stream: {Source}", sourceLabel);
                return null;
            }
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    public async Task<string?> ExtractTextFromFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("PDF file not found: {Path}", filePath);
            return null;
        }

        try
        {
            return await Task.Run(() =>
            {
                using var doc = PdfDocument.Open(filePath);
                var sb = new StringBuilder();
                foreach (var page in doc.GetPages())
                {
                    ct.ThrowIfCancellationRequested();
                    sb.AppendLine(page.Text);
                }
                return sb.ToString();
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF file: {Path}", filePath);
            return null;
        }
    }
}