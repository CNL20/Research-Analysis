namespace ScholarTrend.Application.DTOs.Sync;

public class ApproveSyncRequest
{
    /// <summary>
    /// Null or empty = approve all pending papers in the proposal.
    /// </summary>
    public List<int>? PendingPaperIds { get; set; }
}
