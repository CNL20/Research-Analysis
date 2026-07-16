namespace ScholarTrend.Domain.Constants;

public static class PaperDownloadStatus
{
    public const string Queued = "Queued";
    public const string Downloading = "Downloading";
    public const string Ready = "Ready";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";

    public static class AccessTypes
    {
        public const string ArXiv = "ArXiv";
        public const string OpenAccess = "OpenAccess";
        public const string Publisher = "Publisher";
        public const string Closed = "Closed";
    }
}
