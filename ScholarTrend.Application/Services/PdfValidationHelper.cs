namespace ScholarTrend.Application.Services;

/// <summary>
/// Validation dùng chung cho PDF download flow — dùng bởi:
///   - PaperPdfDownloadService (channel/background)
///   - PaperPdfDownloadOrchestrator (on-demand khi user xin analyze)
///
/// Tránh duplicate logic và đảm bảo ngưỡng validation giống nhau ở cả 2 path.
/// </summary>
public static class PdfValidationHelper
{
    /// <summary>PDF file header magic bytes (%PDF-) = 0x25 0x50 0x44 0x46</summary>
    public static readonly byte[] PdfMagicBytes = [0x25, 0x50, 0x44, 0x46];

    /// <summary>50 MB — đồng bộ với PaperPdfDownloadService cũ.</summary>
    public const long MaxFileBytes = 50L * 1024 * 1024;

    /// <summary>
    /// Validate đầy đủ một response PDF: URL safety → size → magic-bytes.
    /// Trả về lỗi mô tả cụ thể hoặc null nếu OK.
    /// </summary>
    public static string? ValidateDownloadedPdf(
        string sourceUrl,
        byte[]? bytes,
        string? contentType = null)
    {
        // 1. URL safety (SSRF protection)
        if (!PdfUrlValidator.IsSafe(sourceUrl, out var urlError))
        {
            return $"URL validation failed: {urlError}";
        }

        // 2. Null/empty response
        if (bytes == null || bytes.Length == 0)
        {
            return "Downloaded bytes are null or empty";
        }

        // 3. Size limit
        if (bytes.LongLength > MaxFileBytes)
        {
            return $"PDF exceeds {(MaxFileBytes / 1024 / 1024)} MB limit (got {bytes.LongLength:N0} bytes)";
        }

        // 4. Magic-bytes check (%PDF-)
        if (!HasPdfMagicHeader(bytes))
        {
            // Bonus: detect HTML response to produce a clearer error message
            var hint = LooksLikeHtml(bytes) ? " (response appears to be HTML, not PDF)" : "";
            return $"Response is not a valid PDF (missing %PDF- magic header){hint}";
        }

        return null; // OK
    }

    /// <summary>
    /// Just the %PDF- magic-bytes check, no URL/size validation.
    /// </summary>
    public static bool HasPdfMagicHeader(byte[] bytes)
    {
        if (bytes.Length < 4) return false;
        return bytes[0] == PdfMagicBytes[0]
            && bytes[1] == PdfMagicBytes[1]
            && bytes[2] == PdfMagicBytes[2]
            && bytes[3] == PdfMagicBytes[3];
    }

    /// <summary>
    /// Heuristic: nếu response bắt đầu bằng các tag HTML phổ biến thì khả năng cao
    /// là server trả về trang lỗi HTML thay vì PDF. Chỉ dùng để log/explain, không
    /// dùng để quyết định accept/reject.
    /// </summary>
    private static bool LooksLikeHtml(byte[] bytes)
    {
        if (bytes.Length < 16) return false;
        // Skip leading whitespace
        var i = 0;
        while (i < bytes.Length && (bytes[i] == 0x20 || bytes[i] == 0x09 || bytes[i] == 0x0A || bytes[i] == 0x0D))
            i++;
        if (i + 5 > bytes.Length) return false;

        // Look for "<!DOCTYPE", "<html", "<HTML", "<HEAD"
        return (bytes[i] == 0x3C && (
                   bytes[i + 1] == 0x21 || // <!
                   bytes[i + 1] == 0x68 || // h<tml
                   bytes[i + 1] == 0x48)   // H<TML
               );
    }
}
