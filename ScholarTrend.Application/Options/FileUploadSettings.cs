namespace ScholarTrend.Application.Options;

public class FileUploadSettings
{
    public string StoragePath { get; set; } = "uploads";
    public int MaxImageSizeMb { get; set; } = 5;
    public int MaxDocumentSizeMb { get; set; } = 20;
    public int MaxAvatarSizeMb { get; set; } = 2;
    public int MaxFilesPerUser { get; set; } = 50;
    public string[] AllowedImageTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];
    public string[] AllowedDocumentTypes { get; set; } = ["application/pdf", "text/csv", "application/json"];
}
