namespace ScholarTrend.Application.Interfaces;



/// <summary>

/// Persists paper–keyword links into Keywords + PaperKeywords with normalization,

/// stoplist filtering, and seed-keyword anchoring.

/// </summary>

public interface IPaperKeywordLinkerService

{

    Task LinkKeywordsAsync(int paperId, IEnumerable<string> keywordNames, CancellationToken ct = default);



    Task LinkFromContextAsync(

        int paperId,

        string? title,

        string? abstractText,

        string? syncSearchQuery,

        IEnumerable<string>? apiKeywords,

        CancellationToken ct = default);

}

