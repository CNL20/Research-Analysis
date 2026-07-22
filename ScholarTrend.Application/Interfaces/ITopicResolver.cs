namespace ScholarTrend.Application.Interfaces;

public interface ITopicResolver
{
    /// <summary>
    /// Finds an existing ResearchTopic by name (case-insensitive) or creates one.
    /// Returns null when <paramref name="topicName"/> is blank.
    /// </summary>
    Task<int?> ResolveAsync(string? topicName, CancellationToken ct = default);
}
