using System.Globalization;

namespace ScholarTrend.Application.Services.Keywords;

public static class KeywordLinkRules
{
    public const int MaxKeywordsPerPaper = 8;
    public const int MaxSeedMatchesFromText = 3;

    public static IReadOnlyList<string> SeedKeywordNames { get; } =
    [
        "Artificial Intelligence",
        "Machine Learning",
        "Deep Learning",
        "Data Mining",
        "Computer Vision",
        "Natural Language Processing",
        "Blockchain",
        "Cybersecurity",
        "Big Data",
        "Internet of Things"
    ];

    private static readonly HashSet<string> Stoplist = new(StringComparer.OrdinalIgnoreCase)
    {
        "computer science",
        "medicine",
        "biology",
        "physics",
        "chemistry",
        "engineering",
        "research",
        "study",
        "analysis",
        "review",
        "article",
        "paper",
        "science",
        "general",
        "miscellaneous",
        "other",
        "unknown"
    };

    /// <summary>Maps API / variant names to canonical seed keyword names.</summary>
    private static readonly Dictionary<string, string> SeedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["artificial intelligence"] = "Artificial Intelligence",
        ["ai"] = "Artificial Intelligence",
        ["machine learning"] = "Machine Learning",
        ["ml"] = "Machine Learning",
        ["deep learning"] = "Deep Learning",
        ["data mining"] = "Data Mining",
        ["computer vision"] = "Computer Vision",
        ["natural language processing"] = "Natural Language Processing",
        ["nlp"] = "Natural Language Processing",
        ["blockchain"] = "Blockchain",
        ["cybersecurity"] = "Cybersecurity",
        ["cyber security"] = "Cybersecurity",
        ["big data"] = "Big Data",
        ["internet of things"] = "Internet of Things",
        ["iot"] = "Internet of Things"
    };

    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return string.Join(' ',
            raw.Trim()
                .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries));
    }

    public static string ToDisplayName(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    public static bool IsBlocked(string normalized)
    {
        if (normalized.Length < 3)
        {
            return true;
        }

        return Stoplist.Contains(normalized);
    }

    public static string? ResolveSeedAnchor(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return SeedAliases.TryGetValue(normalized, out var seed) ? seed : null;
    }

    /// <summary>
    /// Normalizes, filters stoplist, maps seed aliases, and dedupes (case-insensitive).
    /// </summary>
    public static IReadOnlyList<string> PrepareKeywordNames(IEnumerable<string> rawNames)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var raw in rawNames)
        {
            var normalized = Normalize(raw);
            if (string.IsNullOrEmpty(normalized) || IsBlocked(normalized))
            {
                continue;
            }

            var display = ResolveSeedAnchor(normalized) ?? ToDisplayName(normalized);
            if (!seen.Add(display))
            {
                continue;
            }

            result.Add(display);
        }

        return result;
    }

    public static IReadOnlyList<string> MatchSeedsFromText(string text)
    {
        var lower = text.ToLowerInvariant();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        foreach (var seed in SeedKeywordNames)
        {
            if (lower.Contains(seed.ToLowerInvariant()) && seen.Add(seed))
            {
                results.Add(seed);
            }
        }

        foreach (var (alias, seed) in SeedAliases)
        {
            if (ContainsToken(lower, alias) && seen.Add(seed))
            {
                results.Add(seed);
            }
        }

        if (lower.Contains("intelligent computing") && seen.Add("Artificial Intelligence"))
        {
            results.Add("Artificial Intelligence");
        }

        return results.Take(MaxSeedMatchesFromText).ToList();
    }

    private static bool ContainsToken(string text, string token)
    {
        if (token.Length >= 4)
        {
            return text.Contains(token);
        }

        return text.Contains($" {token} ") ||
               text.Contains($" {token},") ||
               text.Contains($" {token}.") ||
               text.StartsWith($"{token} ") ||
               text.EndsWith($" {token}") ||
               text.Contains($"-{token}") ||
               text.Contains($"{token}-");
    }
}
