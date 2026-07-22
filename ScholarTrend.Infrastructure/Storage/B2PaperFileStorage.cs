using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Options;

namespace ScholarTrend.Infrastructure.Storage;

/// <summary>
/// Triển khai <see cref="IPaperFileStorage"/> dùng Backblaze B2 (S3-compatible) cho việc lưu PDF bài báo.
/// Khác với <see cref="BackblazeB2StorageService"/> (dùng cho user-upload flow qua <see cref="IFileStorageService"/>):
///   - Lưu dưới key "papers/{paperId}.pdf" (không có userId prefix)
///   - Bucket private — PDF đọc qua signed URL hoặc backend proxy (không public-read)
///   - ResolveAbsolutePath trả URL đầy đủ (https://f005.backblazeb2.com/file/...) để GeminiPdfAnalysisService
///     có thể download lại bằng IDocumentDownloader khi cần extract text
///
/// Retry policy: Polly 3 retries + exponential backoff (2s / 4s / 8s) cho tất cả upload/delete
/// thất bại do transient error (network, 5xx, 429). Không retry cho lỗi logic (4xx khác 429).
/// </summary>
public class B2PaperFileStorage : IPaperFileStorage
{
    private const int RetryCount = 3;
    private const int BaseDelaySeconds = 2;

    private readonly IAmazonS3 _s3Client;
    private readonly BackblazeB2Settings _settings;
    private readonly ILogger<B2PaperFileStorage> _logger;
    private readonly AsyncRetryPolicy _uploadRetryPolicy;

    public B2PaperFileStorage(
        IOptions<BackblazeB2Settings> options,
        ILogger<B2PaperFileStorage> logger)
    {
        _logger = logger;
        _settings = options.Value;

        ValidateSettings(_settings);

        var credentials = new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey);

        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true,
            UseHttp = false,
            SignatureVersion = "4",
            AuthenticationRegion = ExtractRegion(_settings.Endpoint)
        };

        _s3Client = new AmazonS3Client(credentials, config);

        // Polly retry policy: 3 lần, exponential 2s/4s/8s, chỉ retry transient errors
        _uploadRetryPolicy = Policy
            .Handle<AmazonS3Exception>(ex => IsTransientError(ex.StatusCode))
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>(ex => !ct_isCancellationRequested(ex))
            .WaitAndRetryAsync(
                retryCount: RetryCount,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(BaseDelaySeconds, attempt)),
                onRetry: (exception, timespan, attempt, _) =>
                {
                    var s3Status = (exception as AmazonS3Exception)?.StatusCode.ToString() ?? "n/a";
                    _logger.LogWarning(exception,
                        "B2 upload retry {Attempt}/{Max} after {Delay}s (statusCode={StatusCode})",
                        attempt, RetryCount, timespan.TotalSeconds, s3Status);
                });
    }

    public async Task<string> SaveBytesAsync(string relativePath, byte[] bytes, CancellationToken ct)
    {
        var key = NormalizeKey(relativePath);

        await _uploadRetryPolicy.ExecuteAsync(async (innerCt) =>
        {
            using var stream = new MemoryStream(bytes);
            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = "application/pdf"
                // Bucket private — không set CannedACL
            };

            await _s3Client.PutObjectAsync(request, innerCt);
        }, ct);

        _logger.LogDebug("Uploaded PDF to B2: {Bucket}/{Key} ({Bytes} bytes)",
            _settings.BucketName, key, bytes.Length);
        return key;
    }

    public string ResolveAbsolutePath(string relativePath)
    {
        var key = NormalizeKey(relativePath);
        if (string.IsNullOrEmpty(_settings.PublicUrlBase))
        {
            // Fallback: trả key thuần nếu PublicUrlBase chưa config
            return key;
        }
        return $"{_settings.PublicUrlBase.TrimEnd('/')}/{key}";
    }

    public void DeleteIfExists(string relativePath)
    {
        var key = NormalizeKey(relativePath);
        try
        {
            // Retry async delete (không block caller). Nếu fail, log warning — đã best-effort.
            _ = _uploadRetryPolicy.ExecuteAsync(async () =>
            {
                await _s3Client.DeleteObjectAsync(_settings.BucketName, key);
            });
            _logger.LogDebug("Requested delete on B2: {Bucket}/{Key}", _settings.BucketName, key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete PDF at B2 key {Key}", key);
        }
    }

    public async Task<Stream?> OpenReadAsync(string relativePath, CancellationToken ct)
    {
        var key = NormalizeKey(relativePath);

        try
        {
            var response = await _s3Client.GetObjectAsync(_settings.BucketName, key, ct);
            // Caller chịu trách nhiệm dispose stream
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("PDF not found in B2: {Bucket}/{Key}", _settings.BucketName, key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open PDF from B2: {Bucket}/{Key}", _settings.BucketName, key);
            throw;
        }
    }

    public async Task<byte[]?> ReadAllBytesAsync(string relativePath, CancellationToken ct)
    {
        var key = NormalizeKey(relativePath);

        try
        {
            // GetObjectAsync trả ResponseStream — đọc vào MemoryStream rồi ToArray.
            // Dùng retry policy để chống transient B2 errors.
            return await _uploadRetryPolicy.ExecuteAsync(async (innerCt) =>
            {
                var response = await _s3Client.GetObjectAsync(_settings.BucketName, key, innerCt);
                using var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms, innerCt);
                _logger.LogDebug("Downloaded PDF from B2: {Bucket}/{Key} ({Bytes} bytes)",
                    _settings.BucketName, key, ms.Length);
                return ms.ToArray();
            }, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("PDF not found in B2: {Bucket}/{Key}", _settings.BucketName, key);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read PDF bytes from B2: {Bucket}/{Key}", _settings.BucketName, key);
            throw;
        }
    }

    /// <summary>
    /// Xác định lỗi có nên retry không: 5xx server errors + 429 TooManyRequests + RequestTimeout.
    /// </summary>
    private static bool IsTransientError(System.Net.HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 429
            || code == 408   // Request Timeout
            || (code >= 500 && code < 600);
    }

    /// <summary>
    /// Phân biệt giữa cancellation do user/timeout (KHÔNG retry) và TaskCanceledException do network.
    /// Polly xử lý cancellation token trước; nếu CT đã cancelled thì không retry.
    /// </summary>
    private static bool ct_isCancellationRequested(TaskCanceledException ex)
    {
        return ex.CancellationToken.IsCancellationRequested;
    }

    private static string NormalizeKey(string relativePath)
    {
        return relativePath.Replace('\\', '/').TrimStart('/');
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

    private static string ExtractRegion(string endpoint)
    {
        try
        {
            var uri = new Uri(endpoint);
            var parts = uri.Host.Split('.');
            if (parts.Length >= 3 && parts[0] == "s3")
                return parts[1];
            return "us-east-005";
        }
        catch
        {
            return "us-east-005";
        }
    }
}
