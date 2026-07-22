using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Options;

namespace ScholarTrend.Infrastructure.Storage;

/// <summary>
/// Triển khai <see cref="IFileStorageService"/> dùng Backblaze B2 qua S3-compatible API.
/// Bucket phải được tạo ở region tương ứng với <see cref="BackblazeB2Settings.Endpoint"/>.
/// </summary>
public class BackblazeB2StorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly BackblazeB2Settings _settings;

    public BackblazeB2StorageService(IOptions<BackblazeB2Settings> options)
    {
        _settings = options.Value;

        ValidateSettings(_settings);

        var credentials = new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey);

        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true,               // BẮT BUỘC cho Backblaze B2
            UseHttp = false,
            SignatureVersion = "4",              // B2 yêu cầu SigV4
            AuthenticationRegion = ExtractRegion(_settings.Endpoint) // us-east-005 / us-west-002 / ...
        };

        _s3Client = new AmazonS3Client(credentials, config);
    }

    public async Task<string> SaveAsync(
        string userId,
        string storedFileName,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var key = GetObjectKey(userId, storedFileName);

        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            InputStream = content,
            ContentType = GetContentType(storedFileName)
            // KHÔNG set CannedACL -> bucket là Private, không cho public read.
        };

        await _s3Client.PutObjectAsync(request, cancellationToken);
        return key;
    }

    public async Task<Stream> OpenReadAsync(
        string userId,
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        var key = GetObjectKey(userId, storedFileName);

        try
        {
            var response = await _s3Client.GetObjectAsync(
                _settings.BucketName,
                key,
                cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("Stored file was not found in Backblaze B2.", key);
        }
    }

    public async Task DeleteAsync(
        string userId,
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        var key = GetObjectKey(userId, storedFileName);

        try
        {
            await _s3Client.DeleteObjectAsync(
                _settings.BucketName,
                key,
                cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // File không tồn tại -> bỏ qua.
        }
    }

    /// <summary>
    /// Tạo pre-signed URL có thời hạn để download file trong bucket private.
    /// </summary>
    public string GetSignedUrl(string userId, string storedFileName, int expirationMinutes = 60)
    {
        var key = GetObjectKey(userId, storedFileName);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            Protocol = Protocol.HTTPS
        };

        return _s3Client.GetPreSignedURL(request);
    }

    private static string GetObjectKey(string userId, string storedFileName)
    {
        var safeUserId = string.Concat(userId.Split(Path.GetInvalidFileNameChars()));
        return $"{safeUserId}/{storedFileName}";
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".csv" => "text/csv",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    private static void ValidateSettings(BackblazeB2Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint))
            throw new InvalidOperationException("Backblaze B2: 'Endpoint' chưa được cấu hình trong appsettings.json (FileUpload:B2:Endpoint).");

        if (string.IsNullOrWhiteSpace(settings.AccessKey))
            throw new InvalidOperationException("Backblaze B2: 'AccessKey' chưa được cấu hình trong appsettings.json (FileUpload:B2:AccessKey).");

        if (string.IsNullOrWhiteSpace(settings.SecretKey))
            throw new InvalidOperationException("Backblaze B2: 'SecretKey' chưa được cấu hình trong appsettings.json (FileUpload:B2:SecretKey).");

        if (string.IsNullOrWhiteSpace(settings.BucketName))
            throw new InvalidOperationException("Backblaze B2: 'BucketName' chưa được cấu hình trong appsettings.json (FileUpload:B2:BucketName).");
    }

    /// <summary>
    /// Trích xuất region từ ServiceURL, ví dụ:
    /// "https://s3.us-east-005.backblazeb2.com" -> "us-east-005"
    /// </summary>
    private static string ExtractRegion(string endpoint)
    {
        try
        {
            var uri = new Uri(endpoint);
            var host = uri.Host; // s3.us-east-005.backblazeb2.com
            var parts = host.Split('.');
            if (parts.Length >= 3 && parts[0] == "s3")
            {
                return parts[1]; // us-east-005
            }
            return "us-east-005"; // fallback mặc định
        }
        catch
        {
            return "us-east-005";
        }
    }
}
