using ScholarTrend.Application.DTOs.TopicInsights;

namespace ScholarTrend.Application.Services;

public class SectionExtractor
{
    private readonly Dictionary<string, string[]> _sectionPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["introduction"] = new[] { "1. introduction", "introduction", "i. introduction", "background" },
        ["methodology"] = new[] { "method", "approach", "proposed", "architecture", "framework", "algorithm" },
        ["experiment"] = new[] { "experiment", "evaluation", "benchmark", "result", "dataset", "setup" },
        ["discussion"] = new[] { "discussion", "limitation", "weakness", "challenges" },
        ["conclusion"] = new[] { "conclusion", "future work", "summary", "concluding" }
    };

    private readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "this", "that", "from", "which", "have", "has"
    };

    public ExtractedSectionsDto ExtractRelevantSections(string fullText, HashSet<string>? topicKeywords = null)
    {
        if (string.IsNullOrWhiteSpace(fullText))
            return new ExtractedSectionsDto();

        var sections = new Dictionary<string, List<string>>
        {
            ["introduction"] = new(),
            ["methodology"] = new(),
            ["experiment"] = new(),
            ["discussion"] = new(),
            ["conclusion"] = new()
        };

        var lines = fullText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var currentSection = "unknown";
        var sectionLineCount = new Dictionary<string, int>
        {
            ["introduction"] = 0,
            ["methodology"] = 0,
            ["experiment"] = 0,
            ["discussion"] = 0,
            ["conclusion"] = 0
        };

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.Length < 30)
                continue;

            var lowerLine = trimmedLine.ToLowerInvariant();

            if (IsSectionHeader(lowerLine, trimmedLine))
            {
                currentSection = DetectSection(lowerLine, trimmedLine);
            }
            else if (sections.ContainsKey(currentSection) && sectionLineCount[currentSection] < 100)
            {
                if (IsRelevantContent(trimmedLine, topicKeywords))
                {
                    sections[currentSection].Add(trimmedLine);
                    sectionLineCount[currentSection]++;
                }
            }
        }

        return new ExtractedSectionsDto
        {
            Introduction = string.Join(" ", sections["introduction"].Take(30)),
            Methodology = string.Join(" ", sections["methodology"].Take(60)),
            Experiments = string.Join(" ", sections["experiment"].Take(60)),
            Discussion = string.Join(" ", sections["discussion"].Take(50)),
            Conclusion = string.Join(" ", sections["conclusion"].Take(30))
        };
    }

    public ExtractedSectionsDto ExtractSectionsByGapType(string fullText, string gapType, HashSet<string>? topicKeywords = null)
    {
        var allSections = ExtractRelevantSections(fullText, topicKeywords);

        return gapType.ToLowerInvariant() switch
        {
            "dataset gap" => new ExtractedSectionsDto
            {
                Discussion = allSections.Discussion,
                Experiments = allSections.Experiments,
                Conclusion = allSections.Conclusion
            },
            "method gap" => new ExtractedSectionsDto
            {
                Methodology = allSections.Methodology,
                Experiments = allSections.Experiments
            },
            "evaluation gap" => new ExtractedSectionsDto
            {
                Experiments = allSections.Experiments,
                Conclusion = allSections.Conclusion
            },
            "application gap" => new ExtractedSectionsDto
            {
                Introduction = allSections.Introduction,
                Conclusion = allSections.Conclusion
            },
            "limitation gap" or "temporal gap" or "geographic gap" or "contradiction gap" => new ExtractedSectionsDto
            {
                Discussion = allSections.Discussion,
                Conclusion = allSections.Conclusion
            },
            _ => allSections
        };
    }

    public (string primary, string secondary) GetPrioritySectionsByGapType(string gapType)
    {
        return gapType.ToLowerInvariant() switch
        {
            "dataset gap" => ("experiments", "discussion"),
            "method gap" => ("methodology", "experiment"),
            "evaluation gap" => ("experiments", "conclusion"),
            "application gap" => ("introduction", "conclusion"),
            "limitation gap" => ("discussion", "conclusion"),
            "temporal gap" => ("introduction", "conclusion"),
            "geographic gap" => ("introduction", "discussion"),
            "contradiction gap" => ("discussion", "conclusion"),
            _ => ("discussion", "conclusion")
        };
    }

    public string BuildTargetedPrompt(string topicName, ExtractedSectionsDto sections, string targetGapType)
    {
        var (primary, secondary) = GetPrioritySectionsByGapType(targetGapType);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Analyze research gaps for topic: '{topicName}'");
        sb.AppendLine($"Target Gap Type: {targetGapType}");
        sb.AppendLine();
        sb.AppendLine("RELEVANT SECTIONS:");
        sb.AppendLine(new string('-', 50));

        if (primary == "experiments" || secondary == "experiments")
            sb.AppendLine($"EXPERIMENTS/DATASET:\n{sections.Experiments}\n");

        if (primary == "methodology" || secondary == "methodology")
            sb.AppendLine($"METHODOLOGY:\n{sections.Methodology}\n");

        if (primary == "discussion" || secondary == "discussion")
            sb.AppendLine($"DISCUSSION:\n{sections.Discussion}\n");

        if (primary == "conclusion" || secondary == "conclusion")
            sb.AppendLine($"CONCLUSION/FUTURE WORK:\n{sections.Conclusion}\n");

        if (primary == "introduction" || secondary == "introduction")
            sb.AppendLine($"INTRODUCTION:\n{sections.Introduction}\n");

        sb.AppendLine(new string('-', 50));
        sb.AppendLine();
        sb.AppendLine($"Extract: methods, datasets, limitations, future_work relevant to {targetGapType}.");
        sb.AppendLine($"Focus on: {GetFocusHint(targetGapType)}");
        sb.AppendLine();
        sb.AppendLine("Return JSON with methods, datasets, limitations, future_work arrays.");

        return sb.ToString();
    }

    private string GetFocusHint(string gapType)
    {
        return gapType.ToLowerInvariant() switch
        {
            "dataset gap" => "missing datasets, insufficient data diversity, lack of benchmarks, data collection challenges",
            "method gap" => "missing methodologies, algorithmic improvements needed, novel approaches",
            "evaluation gap" => "missing evaluation metrics, insufficient benchmarks, lack of standardized evaluation",
            "application gap" => "real-world applications, practical deployments, industry adoption challenges",
            "limitation gap" => "explicit limitations mentioned by authors, scalability issues, generalizability concerns",
            "temporal gap" => "time-based studies, longitudinal analysis, historical data coverage",
            "geographic gap" => "regional studies, cross-cultural validation, location-specific challenges",
            "contradiction gap" => "conflicting results, disagreements in findings, controversial approaches",
            _ => "any research gaps"
        };
    }

    private bool IsSectionHeader(string lowerLine, string originalLine)
    {
        if (originalLine.Length > 150 || originalLine.Length < 10)
            return false;

        var cleanLine = lowerLine.Trim();
        if (cleanLine.Length > 100)
            return false;

        var patterns = new[]
        {
            @"^\d+\.\s*(introduction|background|related work)",
            @"^(i|ii|iii|iv|v|vi)\.\s*(introduction|background)",
            @"^\d+\.\s*(method|methodology|approach|proposed)",
            @"^\d+\.\s*(experiment|evaluation|result|benchmark)",
            @"^\d+\.\s*(discussion|limitation|challenge)",
            @"^\d+\.\s*(conclusion|summary|future)"
        };

        foreach (var pattern in patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(cleanLine, pattern))
                return true;
        }

        if (cleanLine.StartsWith("#"))
            return true;

        if (cleanLine.EndsWith(":") && !lowerLine.Contains("et al") && !lowerLine.Contains("fig"))
            return true;

        return false;
    }

    private string DetectSection(string lowerLine, string originalLine)
    {
        foreach (var (section, patterns) in _sectionPatterns)
        {
            if (patterns.Any(p => lowerLine.Contains(p)))
                return section;
        }

        return "unknown";
    }

    private bool IsRelevantContent(string line, HashSet<string>? keywords)
    {
        if (line.Length < 50)
            return false;

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 10)
            return false;

        if (keywords != null && keywords.Count > 0)
        {
            var matchedKeywords = words.Count(w => keywords.Contains(w.ToLowerInvariant()));
            if (matchedKeywords >= 2)
                return true;
        }

        var hasMeaningfulWord = words.Any(w => w.Length > 5 && !_stopWords.Contains(w));
        return hasMeaningfulWord;
    }

    public string TruncateWithContext(string text, int maxLength, int contextSentences = 2)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            return text;

        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        if (sentences.Length <= contextSentences * 2)
            return text.Substring(0, Math.Min(text.Length, maxLength)) + "...";

        var result = new List<string>();
        var currentLength = 0;

        foreach (var sentence in sentences)
        {
            if (currentLength + sentence.Length > maxLength && result.Count >= contextSentences)
                break;

            result.Add(sentence);
            currentLength += sentence.Length + 1;
        }

        return string.Join(".", result) + "...";
    }
}
