namespace ScholarTrend.Application.Interfaces;

public interface IJournalResolver
{
    Task<int?> ResolveAsync(string? journalName, CancellationToken ct = default);
}
