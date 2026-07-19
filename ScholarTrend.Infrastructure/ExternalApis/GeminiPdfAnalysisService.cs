using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Constants;
using ScholarTrend.Domain.Entities;
using UglyToad.PdfPig;

namespace ScholarTrend.Infrastructure.ExternalApis;

public class GeminiPdfAnalysisService : IPdfAnalysisService
{
    private readonly IPaperPdfFileRepository _pdfFileRepo;
    private readonly IResearchPaperRepository _paperRepo;
    private readonly IPaperFileStorage _fileStorage;
    private readonly IDocumentDownloader _downloader;
    private readonly IAiExtractionService _aiExtractionService;
    private readonly ILogger<GeminiPdfAnalysisService> _logger;

    private const int MaxTextChars = 150000;

    public GeminiPdfAnalysisService(
        IPaperPdfFileRepository pdfFileRepo,
        IResearchPaperRepository paperRepo,
        IPaperFileStorage fileStorage,
        IDocumentDownloader downloader,
        IAiExtractionService aiExtractionService,
        ILogger<GeminiPdfAnalysisService> logger)
    {
        _pdfFileRepo = pdfFileRepo;
        _paperRepo = paperRepo;
        _fileStorage = fileStorage;
        _downloader = downloader;
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

        // Create entity if not exists — always use the same instance from DB
        if (pdfFile == null)
        {
            pdfFile = new PaperPdfFile
            {
                ResearchPaperId = researchPaperId,
                SourceUrl = paper.PdfUrl,
                ExternalSource = DetectSource(paper.PdfUrl),
                Status = PaperDownloadStatus.Queued,
                AnalysisStatus = PdfAnalysisStatus.Pending
            };
            await _pdfFileRepo.AddAsync(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            _logger.LogInformation("Created new PaperPdfFile entry for paper {Id}", researchPaperId);
        }

        // Download PDF if not already on disk
        await EnsureLocalPdfAsync(pdfFile, cancellationToken);

        // Re-fetch from DB to ensure we have latest state (avoids tracking conflicts)
        pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);
        if (pdfFile == null)
        {
            _logger.LogError("PaperPdfFile disappeared for paper {Id}", researchPaperId);
            return null;
        }

        if (string.IsNullOrEmpty(pdfFile.LocalRelativePath))
        {
            pdfFile.AnalysisStatus = PdfAnalysisStatus.Failed;
            pdfFile.AnalysisError = "PDF gặp trục trặc";
            _logger.LogWarning("PDF download failed for paper {Id}. FailureReason: {Reason}",
                researchPaperId, pdfFile.FailureReason);
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            return null;
        }

        var localPath = _fileStorage.ResolveAbsolutePath(pdfFile.LocalRelativePath);
        if (!File.Exists(localPath))
        {
            pdfFile.AnalysisStatus = PdfAnalysisStatus.Failed;
            pdfFile.AnalysisError = "PDF gặp trục trặc";
            _logger.LogWarning("PDF file not found on disk for paper {Id}. Path: {Path}", researchPaperId, localPath);
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            return null;
        }

        // Already extracted text? Return cached.
        if (!string.IsNullOrWhiteSpace(pdfFile.ExtractedText))
        {
            _logger.LogInformation("Using cached extracted text for paper {Id}", researchPaperId);
        }
        else
        {
            // Extract text
            pdfFile.AnalysisStatus = PdfAnalysisStatus.Extracting;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();

            string text;
            try
            {
                text = await ExtractTextFromPdfAsync(localPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract text from PDF for paper {Id}", researchPaperId);
                pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);
                pdfFile!.AnalysisStatus = PdfAnalysisStatus.TextNotFound;
                pdfFile.AnalysisError = "PDF gặp trục trặc";
                _pdfFileRepo.Update(pdfFile);
                await _pdfFileRepo.SaveChangesAsync();
                return null;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("PDF produced empty text for paper {Id}", researchPaperId);
                pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);
                pdfFile!.AnalysisStatus = PdfAnalysisStatus.TextNotFound;
                pdfFile.AnalysisError = "PDF gặp trục trặc";
                _pdfFileRepo.Update(pdfFile);
                await _pdfFileRepo.SaveChangesAsync();
                return null;
            }

            pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);
            pdfFile!.ExtractedText = text.Length > MaxTextChars ? text[..MaxTextChars] : text;
            pdfFile.ExtractedAt = DateTime.UtcNow;
            pdfFile.AnalysisStatus = PdfAnalysisStatus.Analyzing;
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
        }

        // Re-fetch one more time before Gemini call
        pdfFile = await _pdfFileRepo.GetByResearchPaperIdAsync(researchPaperId);

        // Analyze with Gemini
        _logger.LogInformation("Sending PDF text to AI for paper {Id}. Text length: {Len}",
            researchPaperId, pdfFile?.ExtractedText?.Length ?? 0);
        
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

    private async Task EnsureLocalPdfAsync(PaperPdfFile pdfFile, CancellationToken ct)
    {
        // Already have a valid local file?
        if (!string.IsNullOrEmpty(pdfFile.LocalRelativePath))
        {
            var localPath = _fileStorage.ResolveAbsolutePath(pdfFile.LocalRelativePath);
            if (File.Exists(localPath))
            {
                _logger.LogInformation("PDF already exists on disk for paper {Id}: {Path}",
                    pdfFile.ResearchPaperId, localPath);
                return;
            }
            // File was deleted — re-download
            _logger.LogWarning("PDF file missing on disk for paper {Id}, re-downloading. Path: {Path}",
                pdfFile.ResearchPaperId, localPath);
        }
        else
        {
            _logger.LogInformation("No local PDF for paper {Id}, downloading from {Url}",
                pdfFile.ResearchPaperId, pdfFile.SourceUrl);
        }

        pdfFile.Status = PaperDownloadStatus.Downloading;
        _pdfFileRepo.Update(pdfFile);
        await _pdfFileRepo.SaveChangesAsync();

        var doc = await _downloader.DownloadAsync(pdfFile.SourceUrl, ct);
        if (doc == null)
        {
            pdfFile.FailureReason = $"Download returned null. URL: {pdfFile.SourceUrl}";
            pdfFile.Status = PaperDownloadStatus.Failed;
            _logger.LogWarning("Download failed for paper {Id}. Reason: {Reason}",
                pdfFile.ResearchPaperId, pdfFile.FailureReason);
            _pdfFileRepo.Update(pdfFile);
            await _pdfFileRepo.SaveChangesAsync();
            return;
        }

        var relativePath = $"papers/{pdfFile.ResearchPaperId}.pdf";
        var sha256 = ComputeSha256(doc.Bytes);

        await _fileStorage.SaveBytesAsync(relativePath, doc.Bytes, ct);

        pdfFile.LocalRelativePath = relativePath;
        pdfFile.SizeBytes = doc.Bytes.LongLength;
        pdfFile.ContentType = doc.ContentType ?? "application/pdf";
        pdfFile.Sha256 = sha256;
        pdfFile.Status = PaperDownloadStatus.Ready;
        pdfFile.CompletedAt = DateTime.UtcNow;
        pdfFile.AttemptCount++;

        _pdfFileRepo.Update(pdfFile);
        await _pdfFileRepo.SaveChangesAsync();

        _logger.LogInformation("PDF downloaded and saved for paper {Id}. Size: {Size} bytes",
            pdfFile.ResearchPaperId, doc.Bytes.LongLength);
    }

    private static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ExtractTextFromPdfAsync(string filePath, CancellationToken ct)
    {
        var sb = new StringBuilder();

        await Task.Run(() =>
        {
            using var doc = PdfDocument.Open(filePath);
            foreach (var page in doc.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                sb.AppendLine(page.Text);
            }
        }, ct);

        return sb.ToString();
    }

    private static string DetectSource(string url)
    {
        var lower = url.ToLowerInvariant();
        if (lower.Contains("arxiv.org")) return PaperDownloadStatus.AccessTypes.ArXiv;
        if (lower.Contains("openaccess") || lower.Contains("doi.org")) return PaperDownloadStatus.AccessTypes.OpenAccess;
        return PaperDownloadStatus.AccessTypes.Publisher;
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
