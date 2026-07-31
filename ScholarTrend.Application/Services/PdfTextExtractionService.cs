using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.Pdf;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

/// <summary>
/// Trích xuất text từ PDFs đã download — phục vụ gap analysis.
///
/// Tại sao cần service riêng (không dùng trực tiếp IPdfAnalysisService.Gemini):
///   - IPdfAnalysisService.AnalyzePdfAsync chạy cả PDF text extraction + Gemini analysis (chậm, tốn token).
///   - Gap analysis cần RAW TEXT của NHIỀU paper, không cần AI analysis từng paper.
///   - Service này chỉ parse PDF → text, cache vào PaperPdfFile.ExtractedText.
///
/// Flow:
///   1. Tìm PaperPdfFile theo ResearchPaperId (Status phải = Ready).
///   2. Nếu đã có ExtractedText → skip (cache).
///   3. Download PDF từ storage (B2/Local) → IPaperTextExtractor → raw text.
///   4. Lưu text + ExtractedAt vào PaperPdfFile.
/// </summary>
public class PdfTextExtractionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaperFileStorageProvider _storageProvider;
    private readonly IFileStorageService _userFileStorage;
    private readonly IPaperTextExtractor _textExtractor;
    private readonly IPaperQualityService _paperQualityService;
    private readonly ILogger<PdfTextExtractionService> _logger;

    public PdfTextExtractionService(
        IUnitOfWork unitOfWork,
        IPaperFileStorageProvider storageProvider,
        IFileStorageService userFileStorage,
        IPaperTextExtractor textExtractor,
        IPaperQualityService paperQualityService,
        ILogger<PdfTextExtractionService> logger)
    {
        _unitOfWork = unitOfWork;
        _storageProvider = storageProvider;
        _userFileStorage = userFileStorage;
        _textExtractor = textExtractor;
        _paperQualityService = paperQualityService;
        _logger = logger;
    }

    /// <summary>
    /// Trích xuất text cho 1 paper. Idempotent — cache hit sẽ skip.
    /// </summary>
    public async Task<PdfExtractionResultDto> ExtractForPaperAsync(int researchPaperId, bool forceReExtract = false, CancellationToken ct = default)
    {
        var pdfFile = await _unitOfWork.PaperPdfFiles.GetByResearchPaperIdAsync(researchPaperId);
        
        // Always check if there is a community uploaded PDF
        var userFiles = await _unitOfWork.UserFiles.GetByPaperIdAsync(researchPaperId);
        var userPdf = userFiles.FirstOrDefault(f => f.Category == FileCategories.Document && f.ContentType == "application/pdf" && !f.IsDeleted);

        // If user uploaded a PDF, ensure PaperPdfFile points to it and is Ready
        if (userPdf != null && (pdfFile == null || pdfFile.Status != PaperDownloadStatus.Ready || !pdfFile.LocalRelativePath.StartsWith("USER_FILE|")))
        {
            if (pdfFile == null)
            {
                pdfFile = new PaperPdfFile
                {
                    ResearchPaperId = researchPaperId,
                    LocalRelativePath = $"USER_FILE|{userPdf.UserId}|{userPdf.StoredFileName}",
                    SizeBytes = userPdf.SizeBytes,
                    ContentType = userPdf.ContentType,
                    Status = PaperDownloadStatus.Ready,
                    CompletedAt = DateTime.UtcNow
                };
                await _unitOfWork.PaperPdfFiles.AddAsync(pdfFile);
            }
            else
            {
                pdfFile.LocalRelativePath = $"USER_FILE|{userPdf.UserId}|{userPdf.StoredFileName}";
                pdfFile.SizeBytes = userPdf.SizeBytes;
                pdfFile.ContentType = userPdf.ContentType;
                pdfFile.Status = PaperDownloadStatus.Ready;
                pdfFile.FailureReason = null;
                pdfFile.CompletedAt = DateTime.UtcNow;
                _unitOfWork.PaperPdfFiles.Update(pdfFile);
            }
            await _unitOfWork.SaveChangesAsync();
        }

        if (pdfFile == null)
        {
            return new PdfExtractionResultDto
            {
                ResearchPaperId = researchPaperId,
                Status = "Failed",
                ErrorMessage = "No PaperPdfFile or UserFile record found for this paper.",
                ExtractedAt = DateTime.UtcNow
            };
        }

        if (pdfFile.Status != PaperDownloadStatus.Ready)
        {
            return new PdfExtractionResultDto
            {
                ResearchPaperId = researchPaperId,
                Status = "Failed",
                ErrorMessage = $"PDF not ready (status={pdfFile.Status}).",
                ExtractedAt = DateTime.UtcNow
            };
        }

        // Cache hit (trừ khi force re-extract)
        if (!forceReExtract && !string.IsNullOrWhiteSpace(pdfFile.ExtractedText))
        {
            // Đảm bảo PaperQuality luôn được update kể cả khi hit cache
            await _paperQualityService.AssessPaperAsync(researchPaperId);

            return new PdfExtractionResultDto
            {
                ResearchPaperId = researchPaperId,
                LocalRelativePath = pdfFile.LocalRelativePath,
                ExtractedText = pdfFile.ExtractedText,
                CharacterCount = pdfFile.ExtractedText.Length,
                Status = "Extracted",
                ExtractedAt = pdfFile.ExtractedAt ?? DateTime.UtcNow
            };
        }

        // Resolve storage + extract
        var storage = _storageProvider.GetActiveStorage();
        var storageType = storage.GetType().Name;

        string? text = null;
        string? error = null;

        try
        {
            if (pdfFile.LocalRelativePath.StartsWith("USER_FILE|"))
            {
                var parts = pdfFile.LocalRelativePath.Split('|');
                if (parts.Length == 3)
                {
                    var userId = parts[1];
                    var storedFileName = parts[2];
                    Stream? stream = await _userFileStorage.OpenReadAsync(userId, storedFileName, ct);
                    if (stream == null)
                    {
                        error = $"User PDF not found in storage: {pdfFile.LocalRelativePath}";
                    }
                    else
                    {
                        await using (stream)
                        {
                            text = await _textExtractor.ExtractTextAsync(stream, storedFileName, ct);
                        }
                    }
                }
                else
                {
                    error = $"Invalid USER_FILE format: {pdfFile.LocalRelativePath}";
                }
            }
            else
            {
                // Local: đọc file path trực tiếp (nhanh, ít memory).
                // B2 hoặc stream-based: download về MemoryStream rồi parse.
                //
                // Phân biệt bằng class name (không cần using tới Infrastructure — giữ Application layer clean).
                // - LocalPaperFileStorage: có ResolveAbsolutePath → đọc file path.
                // - B2PaperFileStorage / khác: dùng OpenReadAsync → Stream.
                const string LocalStorageName = "LocalPaperFileStorage";

                if (storageType == LocalStorageName)
                {
                    // Reflection: gọi ResolveAbsolutePath qua IPaperFileStorage interface (đã có sẵn).
                    var filePath = storage.ResolveAbsolutePath(pdfFile.LocalRelativePath);
                    if (!File.Exists(filePath))
                    {
                        error = $"PDF file missing on disk: {filePath}";
                    }
                    else
                    {
                        text = await _textExtractor.ExtractTextFromFileAsync(filePath, ct);
                    }
                }
                else
                {
                    // B2 hoặc stream-based storage.
                    Stream? stream = await storage.OpenReadAsync(pdfFile.LocalRelativePath, ct);
                    if (stream == null)
                    {
                        error = $"PDF not found in storage: {pdfFile.LocalRelativePath}";
                    }
                    else
                    {
                        await using (stream)
                        {
                            text = await _textExtractor.ExtractTextAsync(stream, pdfFile.LocalRelativePath, ct);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text for paper {Id}", researchPaperId);
            error = $"{ex.GetType().Name}: {ex.Message}";
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new PdfExtractionResultDto
            {
                ResearchPaperId = researchPaperId,
                LocalRelativePath = pdfFile.LocalRelativePath,
                Status = string.IsNullOrEmpty(error) ? "Empty" : "Failed",
                ErrorMessage = error ?? "PDF parsed but produced no text (might be scanned/image-only).",
                ExtractedAt = DateTime.UtcNow
            };
        }

        // Cache vào DB (strip null bytes — Postgres UTF-8 rejects 0x00)
        text = PdfValidationHelper.SanitizeForPostgres(text)!;
        pdfFile.ExtractedText = text;
        pdfFile.ExtractedAt = DateTime.UtcNow;
        _unitOfWork.PaperPdfFiles.Update(pdfFile);
        await _unitOfWork.PaperPdfFiles.SaveChangesAsync();

        // Trigger quality assessment to update AnalysisLevel to FullText
        await _paperQualityService.AssessPaperAsync(researchPaperId);

        _logger.LogInformation(
            "Extracted {Chars:N0} chars from PDF for paper {Id} (storage={Storage})",
            text.Length, researchPaperId, storageType);

        return new PdfExtractionResultDto
        {
            ResearchPaperId = researchPaperId,
            LocalRelativePath = pdfFile.LocalRelativePath,
            ExtractedText = text,
            CharacterCount = text.Length,
            Status = "Extracted",
            ExtractedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Lấy raw text đã extract của 1 paper (cho debug / xem trước).
    /// Trả về null nếu chưa extract.
    /// </summary>
    public async Task<string?> GetExtractedTextAsync(int researchPaperId, CancellationToken ct = default)
    {
        var pdfFile = await _unitOfWork.PaperPdfFiles.GetByResearchPaperIdAsync(researchPaperId);
        return pdfFile?.ExtractedText;
    }

    /// <summary>
    /// Trích xuất text cho NHIỀU papers cùng lúc (dùng cho gap analysis seed).
    /// </summary>
    public async Task<PdfBulkExtractionResultDto> ExtractForPapersAsync(
        IEnumerable<int> researchPaperIds,
        bool forceReExtract = false,
        CancellationToken ct = default)
    {
        var ids = researchPaperIds.Distinct().ToList();
        var result = new PdfBulkExtractionResultDto { Requested = ids.Count };

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();

            var item = await ExtractForPaperAsync(id, forceReExtract, ct);
            result.Items.Add(item);

            switch (item.Status)
            {
                case "Extracted": result.Extracted++; break;
                case "Failed":
                case "Empty":    result.Failed++; break;
                default:         result.Skipped++; break;
            }
        }

        return result;
    }

    /// <summary>
    /// Trích xuất text cho tất cả papers đã download PDF (Status=Ready) mà chưa có ExtractedText.
    /// Dùng cho seed/backfill khi mới setup PDF parser.
    /// </summary>
    public async Task<PdfBulkExtractionResultDto> ExtractForAllReadyAsync(int maxPapers = 200, CancellationToken ct = default)
    {
        var ready = await _unitOfWork.PaperPdfFiles.GetByStatusAsync(PaperDownloadStatus.Ready, maxPapers);
        var idsNeedingExtract = ready
            .Where(p => string.IsNullOrWhiteSpace(p.ExtractedText))
            .Select(p => p.ResearchPaperId)
            .ToList();

        _logger.LogInformation(
            "Backfill PDF text extraction: {Total} ready PDFs, {ToExtract} missing extracted text",
            ready.Count, idsNeedingExtract.Count);

        if (idsNeedingExtract.Count == 0)
        {
            return new PdfBulkExtractionResultDto { Requested = 0 };
        }

        return await ExtractForPapersAsync(idsNeedingExtract, forceReExtract: false, ct: ct);
    }
}