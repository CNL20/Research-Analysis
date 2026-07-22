namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Wrapper chọn đúng IPaperFileStorage implementation theo FileUpload:Provider config.
/// Dùng cho PaperPdfDownloadService (ghi file mới) và PapersController.DownloadPdf (đọc file).
///
/// Khác với inject IEnumerable&lt;IPaperFileStorage&gt; ở PdfStorageMigrationService — cái đó cần CẢ 2
/// instances để copy file từ local lên B2.
/// </summary>
public interface IPaperFileStorageProvider
{
    /// <summary>
    /// Trả về storage đang active theo config (B2 hoặc Local).
    /// </summary>
    IPaperFileStorage GetActiveStorage();
}