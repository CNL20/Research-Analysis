using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Application.Services;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Storage;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class GeminiPdfAnalysisService : IPdfAnalysisService
{
    private readonly IPaperPdfFileRepository _pdfFileRepo;
    private readonly IResearchPaperRepository _paperRepo;
    private readonly IPaperFileStorageProvider _storageProvider;
    private readonly IPaperTextExtractor _textExtractor;
    private readonly IPaperPdfDownloadOrchestrator _downloadOrchestrator;
    private readonly IAiExtractionService _aiExtractionService;
    private readonly ILogger<GeminiPdfAnalysisService> _logger;

    private const int MaxTextChars = 150000;

    public GeminiPdfAnalysisService(
        IPaperPdfFileRepository pdfFileRepo,
        IResearchPaperRepository paperRepo,
        IPaperFileStorageProvider storageProvider,
        IPaperTextExtractor textExtractor,
        IPaperPdfDownloadOrchestrator downloadOrchestrator,
        IAiExtractionService aiExtractionService,
        ILogger<GeminiPdfAnalysisService> logger)
    {
        _pdfFileRepo = pdfFileRepo;
        _paperRepo = paperRepo;
        _storageProvider = storageProvider;
        _textExtractor = textExtractor;
        _downloadOrchestrator = downloadOrchestrator;
        _aiExtractionService = aiExtractionService;
        _logger = logger;
    }

    public async Task<AiPaperExtractionDto?> GetCachedAnalysisAsync(int researchPaperId, CancellationToken cancellationToken = default)
    {
        var pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);
        if (pdfFile == null || string.IsNullOrEmpty(pdfFile.AnalysisResultJson))
            return null;

        if (pdfFile.AnalysisStatus == PdfAnalysisStatus.Completed)
        {
            try
            {
                return JsonSerializer.Deserialize<AiPaperExtractionDto>(pdfFile.AnalysisResultJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    public async Task<AiPaperExtractionDto?> AnalyzePdfAsync(int researchPaperId, CancellationToken cancellationToken = default)
    {
        var paper = await _paperRepo.GetByIdAsync(researchPaperId);
        if (paper == null)
        {
            _logger.LogWarning("Paper {Id} not found for PDF analysis", researchPaperId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(paper.PdfUrl))
        {
            _logger.LogWarning("Paper {Id} has no PdfUrl for analysis", researchPaperId);
            return null;
        }

        var pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);

        // Return cached if already successfully analyzed
        if (pdfFile != null && pdfFile.AnalysisStatus == PdfAnalysisStatus.Completed
            && !string.IsNullOrEmpty(pdfFile.AnalysisResultJson))
        {
            _logger.LogInformation("Returning cached PDF analysis for paper {Id}", researchPaperId);
            return JsonSerializer.Deserialize<AiPaperExtractionDto>(pdfFile.AnalysisResultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        // Đảm bảo có PaperPdfFile + đã download PDF thành công.
        // Orchestrator xử lý: tạo entity (nếu chưa), download, validate (magic-bytes + URL safety), lưu vào storage.
        // Nếu fail, FailureReason đã được set trong entity.
        var ensured = await _downloadOrchestrator.EnsurePdfForPaperAsync(researchPaperId, cancellationToken);
        if (ensured == null)
        {
            _logger.LogWarning("PDF orchestrator returned null for paper {Id}", researchPaperId);
            return null;
        }

        // Re-fetch from DB to ensure we have latest state (avoids tracking conflicts)
        pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);
        if (pdfFile == null)
        {
            _logger.LogError("PaperPdfFile disappeared for paper {Id}", researchPaperId);
            return null;
        }

        // *** KEY FIX: nếu download/validation fail, đánh dấu AnalysisStatus = Failed và trả null ***
        if (pdfFile.Status == PaperDownloadStatus.Failed || string.IsNullOrEmpty(pdfFile.LocalRelativePath))
        {
            pdfFile.AnalysisStatus = PdfAnalysisStatus.Failed;
            pdfFile.AnalysisError = pdfFile.FailureReason ?? "PDF download/validation failed";
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            _logger.LogWarning(
                "PDF download failed for paper {Id}. AnalysisStatus=Failed. Reason: {Reason}",
                researchPaperId, pdfFile.FailureReason);
            return null;
        }

        // Lấy storage active (Local hoặc B2). PdfDocument không cần file path thật —
        // dùng IPaperTextExtractor (PdfPig) hỗ trợ cả Stream và file path.
        var storage = _storageProvider.GetActiveStorage();
        string? extractedText = null;

        // Ưu tiên cache (tránh parse lại PDF nặng)
        if (!string.IsNullOrWhiteSpace(pdfFile.ExtractedText))
        {
            _logger.LogInformation("Using cached extracted text for paper {Id}", researchPaperId);
            extractedText = pdfFile.ExtractedText;
        }
        else
        {
            pdfFile.AnalysisStatus = PdfAnalysisStatus.Extracting;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();

            extractedText = await ExtractFromStorageAsync(storage, pdfFile.LocalRelativePath, researchPaperId, cancellationToken);

            if (string.IsNullOrEmpty(extractedText))
            {
                pdfFile.AnalysisStatus = PdfAnalysisStatus.Failed;
                pdfFile.AnalysisError = "PDF parse failed (stream null or PdfPig threw)";
                _pdfFileRepo.Update(pdfFile);
                await _pdfFileRepo.SaveChangesAsync();
                return null;
            }

            // Cache extracted text vào entity để lần sau dùng lại.
            // Strip null bytes — Postgres UTF-8 rejects 0x00 from some PDF extracts.
            extractedText = PdfValidationHelper.SanitizeForPostgres(extractedText)!;
            pdfFile.ExtractedText = extractedText;
            pdfFile.ExtractedAt = DateTime.UtcNow;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            _logger.LogInformation(
                "Extracted {Chars:N0} chars from PDF for paper {Id} (sha256={Sha8})",
                extractedText.Length, researchPaperId, pdfFile.Sha256?[..Math.Min(8, pdfFile.Sha256.Length)]);
        }

        var truncated = extractedText.Length > MaxTextChars ? extractedText[..MaxTextChars] : extractedText;
        truncated = PdfValidationHelper.SanitizeForPostgres(truncated)!;

        // Cache truncated text nếu lần trước chưa cache (chỉ cache 1 lần duy nhất lúc extract)
        if (string.IsNullOrWhiteSpace(pdfFile.ExtractedText) && !string.Equals(pdfFile.ExtractedText, truncated))
        {
            pdfFile.ExtractedText = truncated;
            pdfFile.ExtractedAt = DateTime.UtcNow;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
        }

        var text = truncated;

        // Analyze with Gemini
        _logger.LogInformation("Sending PDF text to AI for paper {Id}. Text length: {Len}",
            researchPaperId, text.Length);
        
        // Extract sections from text
        var extractedSections = ExtractSections(pdfFile!.ExtractedText!);
        
        // Use ExtractFromFullTextAsync for PDF content (more detailed extraction)
        var extraction = await _aiExtractionService.ExtractFromFullTextAsync(pdfFile.ExtractedText!, cancellationToken);

        if (extraction == null)
        {
            pdfFile.AnalysisStatus = PdfAnalysisStatus.Failed;
            pdfFile.AnalysisError = "AI analysis failed";
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            return null;
        }

        // If sections were found, merge them into extraction
        if (!string.IsNullOrEmpty(extractedSections["Discussion"]))
        {
            extraction.Discussions ??= [];
            extraction.Discussions.Insert(0, extractedSections["Discussion"]);
        }
        if (!string.IsNullOrEmpty(extractedSections["Conclusion"]))
        {
            extraction.Conclusions ??= [];
            extraction.Conclusions.Insert(0, extractedSections["Conclusion"]);
        }

        // Infer limitations and future work if extraction returned empty
        var hasLimitations = extraction.Limitations.Count > 0;
        var hasFutureWork = extraction.FutureWork.Count > 0;

        if (!hasLimitations || !hasFutureWork)
        {
            _logger.LogInformation("Extraction returned empty limitations ({L}) or future_work ({F}) for paper {Id}. Triggering AI inference.",
                extraction.Limitations.Count, extraction.FutureWork.Count, researchPaperId);

            var inference = await _aiExtractionService.InferLimitationsAndFutureWorkAsync(
                paper.Title,
                pdfFile.ExtractedText ?? paper.Abstract ?? string.Empty,
                extraction.Methods,
                extraction.Datasets,
                cancellationToken);

            if (!hasLimitations && inference.Limitations.Count > 0)
                extraction.Limitations = inference.Limitations;

            if (!hasFutureWork && inference.FutureWork.Count > 0)
                extraction.FutureWork = inference.FutureWork;
        }

        // Cache result
        pdfFile.AnalysisResultJson = JsonSerializer.Serialize(extraction);
        pdfFile.AnalysisStatus = PdfAnalysisStatus.Completed;
        pdfFile.AnalysisError = null;
        _pdfFileRepo.Update(pdfFile);
        await _pdfFileRepo.SaveChangesAsync();

        _logger.LogInformation("PDF analysis completed and cached for paper {Id}", researchPaperId);
        return extraction;
    }

    private async Task<string?> ExtractFromStorageAsync(
        IPaperFileStorage storage,
        string relativePath,
        int researchPaperId,
        CancellationToken ct)
    {
        if (storage is LocalPaperFileStorage local)
        {
            var filePath = local.ResolveAbsolutePath(relativePath);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("PDF file not found on disk for paper {Id}: {Path}",
                    researchPaperId, filePath);
                return null;
            }
            return await _textExtractor.ExtractTextFromFileAsync(filePath, ct);
        }

        // B2 (hoặc bất kỳ stream-based storage nào): đọc qua stream.
        Stream? stream = await storage.OpenReadAsync(relativePath, ct);
        if (stream == null)
        {
            _logger.LogWarning("PDF stream is null from storage for paper {Id}: {Rel}",
                researchPaperId, relativePath);
            return null;
        }

        await using (stream)
        {
            return await _textExtractor.ExtractTextAsync(stream, relativePath, ct);
        }
    }

    /// <summary>
    /// Extracts sections from PDF text by identifying section headers.
    /// </summary>
    public static Dictionary<string, string> ExtractSections(string text)
    {
        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Discussion"] = "",
            ["Conclusion"] = "",
            ["Future Work"] = "",
            ["Limitations"] = ""
        };

        var lines = text.Split('\n');
        var currentSection = "";
        var currentContent = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            var isHeader = false;

            foreach (var sectionName in sections.Keys)
            {
                if (IsSectionHeader(trimmedLine, sectionName))
                {
                    if (!string.IsNullOrEmpty(currentSection) && currentContent.Any())
                    {
                        sections[currentSection] = string.Join(" ", currentContent).Trim();
                    }
                    currentSection = sectionName;
                    currentContent = new List<string>();
                    isHeader = true;
                    break;
                }
            }

            if (!isHeader && !string.IsNullOrEmpty(currentSection))
            {
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    currentContent.Add(trimmedLine);
                }
            }
        }

        if (!string.IsNullOrEmpty(currentSection) && currentContent.Any())
        {
            sections[currentSection] = string.Join(" ", currentContent).Trim();
        }

        return sections;
    }

    private static bool IsSectionHeader(string line, string sectionName)
    {
        var normalizedLine = line.ToLowerInvariant().Replace(" ", "").Replace("_", "");
        var normalizedSection = sectionName.ToLowerInvariant().Replace(" ", "").Replace("_", "");

        if (normalizedLine == normalizedSection) return true;
        if (normalizedLine == normalizedSection + ":") return true;

        var patterns = new[]
        {
            $"^{normalizedSection}$",
            $"^{normalizedSection}:?$",
            $"\\d+\\.?\\s*{normalizedSection}:?",
            $"[IVX]+\\.?\\s*{normalizedSection}:?",
            $"section\\s*{normalizedSection}:?"
        };

        return patterns.Any(p => 
            System.Text.RegularExpressions.Regex.IsMatch(line, p, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }
}
