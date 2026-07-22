namespace ScholarTrend.Application.Services.Topics;

/// <summary>
/// Maps free-text topic labels from external APIs onto the seeded ResearchTopics names.
/// </summary>
public static class ScholarTopicMapper
{
    public static readonly string[] SeededTopicNames =
    [
        "Artificial Intelligence",
        "Data Science",
        "Software Engineering",
        "Cyber Security",
        "Cloud Computing"
    ];

    /// <summary>
    /// Returns distinct seeded topic names matched from source labels (and optional title/abstract).
    /// </summary>
    public static IReadOnlyList<string> MapToSeededTopics(
        IEnumerable<string>? sourceLabels,
        string? title = null,
        string? abstractText = null)
    {
        var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var blob = string.Join(" ",
                (sourceLabels ?? []).Where(s => !string.IsNullOrWhiteSpace(s))
                    .Append(title ?? string.Empty)
                    .Append(abstractText ?? string.Empty))
            .ToLowerInvariant();

        foreach (var label in sourceLabels ?? [])
        {
            if (string.IsNullOrWhiteSpace(label)) continue;
            var seeded = MatchOne(label);
            if (seeded != null) matched.Add(seeded);
        }

        // Fallback: infer from title/abstract when source had no usable topic labels.
        if (matched.Count == 0 && !string.IsNullOrWhiteSpace(blob))
        {
            foreach (var name in SeededTopicNames)
            {
                if (MatchOne(blob) is { } hit)
                {
                    matched.Add(hit);
                    break;
                }
            }

            // Direct keyword buckets on combined text
            foreach (var name in SeededTopicNames)
            {
                if (TextMatchesTopic(blob, name))
                {
                    matched.Add(name);
                }
            }
        }

        return matched.ToList();
    }

    public static string? MatchOne(string label)
    {
        var text = label.Trim().ToLowerInvariant();
        if (text.Length == 0) return null;

        // Exact / contains seeded name
        foreach (var seeded in SeededTopicNames)
        {
            if (text.Equals(seeded, StringComparison.OrdinalIgnoreCase)
                || text.Contains(seeded.ToLowerInvariant())
                || seeded.ToLowerInvariant().Contains(text))
            {
                return seeded;
            }
        }

        if (TextMatchesTopic(text, "Artificial Intelligence")) return "Artificial Intelligence";
        if (TextMatchesTopic(text, "Data Science")) return "Data Science";
        if (TextMatchesTopic(text, "Software Engineering")) return "Software Engineering";
        if (TextMatchesTopic(text, "Cyber Security")) return "Cyber Security";
        if (TextMatchesTopic(text, "Cloud Computing")) return "Cloud Computing";

        return null;
    }

    private static bool TextMatchesTopic(string text, string seededTopic)
    {
        return seededTopic switch
        {
            "Artificial Intelligence" => ContainsAny(text,
                "artificial intelligence", "machine learning", "deep learning", "neural network",
                "computer vision", "natural language", "nlp", "llm", "transformer", "reinforcement learning",
                "supervised learning", "unsupervised learning", "cnn", "rnn", "gan"),
            "Data Science" => ContainsAny(text,
                "data science", "data mining", "big data", "analytics", "statistics", "data analysis",
                "business intelligence", "data visualization"),
            "Software Engineering" => ContainsAny(text,
                "software engineering", "software development", "devops", "continuous integration",
                "software testing", "software architecture", "requirement engineering", "agile"),
            "Cyber Security" => ContainsAny(text,
                "cyber security", "cybersecurity", "information security", "network security",
                "malware", "intrusion detection", "cryptography", "privacy", "vulnerability"),
            "Cloud Computing" => ContainsAny(text,
                "cloud computing", "distributed system", "kubernetes", "microservices",
                "virtualization", "edge computing", "serverless", "iaas", "paas"),
            _ => false
        };
    }

    private static bool ContainsAny(string text, params string[] needles)
        => needles.Any(n => text.Contains(n, StringComparison.OrdinalIgnoreCase));
}
