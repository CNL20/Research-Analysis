namespace ScholarTrend.Application.Options;

public class BackblazeB2Settings
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = string.Empty;
    public string PublicUrlBase { get; set; } = string.Empty;
}