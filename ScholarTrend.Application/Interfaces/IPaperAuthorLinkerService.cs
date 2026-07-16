namespace ScholarTrend.Application.Interfaces;

public interface IPaperAuthorLinkerService
{
    Task LinkAuthorsAsync(int paperId, IEnumerable<string> authorNames, CancellationToken ct = default);
}
