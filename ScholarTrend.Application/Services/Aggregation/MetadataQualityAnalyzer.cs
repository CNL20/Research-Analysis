using ScholarTrend.Application.DTOs.Aggregation;

namespace ScholarTrend.Application.Services.Aggregation;

public static class MetadataQualityAnalyzer
{
  private static readonly string[] TrackedFields =
  [
    "doi", "title", "authors", "year", "journal", "abstract", "citationCount", "keywords", "pdfUrl"
  ];

  private static readonly Dictionary<string, int> CompletenessWeights = new()
  {
    ["doi"] = 20,
    ["title"] = 15,
    ["authors"] = 15,
    ["abstract"] = 15,
    ["journal"] = 10,
    ["citationCount"] = 10,
    ["keywords"] = 10,
    ["pdfUrl"] = 5,
  };

  private static readonly Dictionary<string, string[]> FieldSourcePriority = new()
  {
    ["doi"] = ["crossref", "openalex", "semantic_scholar", "arxiv", "internal"],
    ["title"] = ["crossref", "openalex", "semantic_scholar", "arxiv", "internal"],
    ["authors"] = ["openalex", "semantic_scholar", "arxiv", "crossref", "internal"],
    ["year"] = ["crossref", "openalex", "semantic_scholar", "arxiv", "internal"],
    ["journal"] = ["crossref", "openalex", "semantic_scholar", "internal", "arxiv"],
    ["abstract"] = ["semantic_scholar", "arxiv", "openalex", "crossref", "internal"],
    ["citationCount"] = ["semantic_scholar", "openalex", "crossref", "internal", "arxiv"],
    ["keywords"] = ["openalex", "semantic_scholar", "internal", "crossref", "arxiv"],
    ["pdfUrl"] = ["internal", "arxiv", "openalex", "semantic_scholar", "crossref"],
  };

  public static PaperAggregateResultDto Analyze(string doi, Dictionary<string, PaperSourceMetadataDto> sources, int? internalPaperId = null)
  {
    var matched = sources.Where(s => s.Value.Found).Select(s => s.Key).ToList();
    var result = new PaperAggregateResultDto
    {
      Doi = doi,
      MatchMethod = "doi",
      InternalPaperId = internalPaperId,
      SourcesAttempted = sources.Keys.ToList(),
      SourcesMatched = matched,
      Sources = sources,
      AggregatedAt = DateTime.UtcNow,
    };

    result.Coverage = BuildCoverage(sources, matched.Count);
    result.DataGaps = BuildDataGaps(result.Coverage, matched.Count);
    result.Conflicts = BuildConflicts(sources, matched);
    result.FieldAuthority = BuildFieldAuthority(sources, matched);
    result.UnifiedMetadata = BuildUnifiedMetadata(result.FieldAuthority, doi);
    result.CompletenessScore = CalculateCompletenessScore(result.UnifiedMetadata);
    result.TrustScore = CalculateTrustScore(result.Conflicts, matched.Count, sources.Count);
    result.ConfidenceLevel = GetConfidenceLevel(result.TrustScore);
    result.Recommendations = BuildRecommendations(result);

    return result;
  }

  public static string NormalizeDoi(string? doi)
  {
    if (string.IsNullOrWhiteSpace(doi))
    {
      return string.Empty;
    }

    var value = doi.Trim();
    const string prefix = "https://doi.org/";
    if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
      value = value[prefix.Length..];
    }

    return value.Trim().ToLowerInvariant();
  }

  private static Dictionary<string, FieldCoverageDto> BuildCoverage(
    Dictionary<string, PaperSourceMetadataDto> sources,
    int matchedCount)
  {
    var coverage = new Dictionary<string, FieldCoverageDto>();
    var total = Math.Max(matchedCount, 1);

    foreach (var field in TrackedFields)
    {
      var presentIn = sources
        .Where(s => s.Value.Found && HasFieldValue(s.Value, field))
        .Select(s => s.Key)
        .ToList();

      var missingIn = sources
        .Where(s => s.Value.Found && !HasFieldValue(s.Value, field))
        .Select(s => s.Key)
        .ToList();

      var count = presentIn.Count;
      coverage[field] = new FieldCoverageDto
      {
        Field = field,
        SourceCount = count,
        TotalSources = total,
        CoveragePercent = Math.Round(count * 100.0 / total, 1),
        PresentIn = presentIn,
        MissingIn = missingIn,
      };
    }

    return coverage;
  }

  private static List<DataGapDto> BuildDataGaps(Dictionary<string, FieldCoverageDto> coverage, int matchedCount)
  {
    if (matchedCount == 0)
    {
      return
      [
        new DataGapDto
        {
          Field = "all",
          CoveragePercent = 0,
          Status = "unavailable",
          Message = "No external source returned metadata for this DOI.",
        },
      ];
    }

    var gaps = new List<DataGapDto>();
    foreach (var item in coverage.Values)
    {
      var status = item.CoveragePercent switch
      {
        >= 80 => "reliable",
        >= 40 => "partial",
        _ => "insufficient",
      };

      if (status == "reliable")
      {
        continue;
      }

      gaps.Add(new DataGapDto
      {
        Field = item.Field,
        CoveragePercent = item.CoveragePercent,
        Status = status,
        Message = status switch
        {
          "partial" => $"Only {item.SourceCount}/{item.TotalSources} sources provide {item.Field}.",
          _ => $"{item.Field} is missing from most sources and was excluded from high-confidence synthesis.",
        },
      });
    }

    return gaps;
  }

  private static List<FieldConflictDto> BuildConflicts(
    Dictionary<string, PaperSourceMetadataDto> sources,
    List<string> matched)
  {
    var conflicts = new List<FieldConflictDto>();

    conflicts.AddRange(CompareTitles(sources, matched));
    conflicts.AddRange(CompareYears(sources, matched));
    conflicts.AddRange(CompareCitations(sources, matched));
    conflicts.AddRange(CompareAuthors(sources, matched));

    return conflicts;
  }

  private static List<FieldConflictDto> CompareTitles(Dictionary<string, PaperSourceMetadataDto> sources, List<string> matched)
  {
    var values = matched
      .Where(s => !string.IsNullOrWhiteSpace(sources[s].Title))
      .ToDictionary(s => s, s => sources[s].Title!);

    if (values.Count < 2)
    {
      return [];
    }

    var baseline = values.Values.First();
    var minSimilarity = values.Values.Min(v => TitleSimilarity(baseline, v));
    if (minSimilarity >= 0.95)
    {
      return [];
    }

    return
    [
      new FieldConflictDto
      {
        Field = "title",
        Status = minSimilarity >= 0.85 ? "minorConflict" : "majorConflict",
        Values = values.ToDictionary(k => k.Key, v => (string?)v.Value),
        Note = $"Title similarity is {minSimilarity:P0} across sources.",
      },
    ];
  }

  private static List<FieldConflictDto> CompareYears(Dictionary<string, PaperSourceMetadataDto> sources, List<string> matched)
  {
    var years = matched
      .Where(s => sources[s].Year.HasValue)
      .Select(s => sources[s].Year!.Value)
      .Distinct()
      .OrderBy(y => y)
      .ToList();

    if (years.Count < 2)
    {
      return [];
    }

    var spread = years[^1] - years[0];
    return
    [
      new FieldConflictDto
      {
        Field = "year",
        Status = spread <= 1 ? "minorConflict" : "majorConflict",
        Values = matched
          .Where(s => sources[s].Year.HasValue)
          .ToDictionary(s => s, s => sources[s].Year!.Value.ToString()),
        Note = spread <= 1
          ? "Publication year differs by at most one year."
          : "Publication year differs significantly between sources.",
      },
    ];
  }

  private static List<FieldConflictDto> CompareCitations(Dictionary<string, PaperSourceMetadataDto> sources, List<string> matched)
  {
    var values = matched
      .Where(s => sources[s].CitationCount.HasValue)
      .ToDictionary(s => s, s => sources[s].CitationCount!.Value);

    if (values.Count < 2)
    {
      return [];
    }

    var max = values.Values.Max();
    var min = values.Values.Min(v => Math.Max(v, 1));
    var percentDiff = Math.Abs(max - min) * 100.0 / min;

    if (percentDiff <= 15)
    {
      return [];
    }

    return
    [
      new FieldConflictDto
      {
        Field = "citationCount",
        Status = percentDiff <= 50 ? "minorConflict" : "majorConflict",
        Values = values.ToDictionary(k => k.Key, v => (string?)v.Value.ToString()),
        Note = $"Citation counts differ by {percentDiff:F1}% — common across bibliographic APIs.",
      },
    ];
  }

  private static List<FieldConflictDto> CompareAuthors(Dictionary<string, PaperSourceMetadataDto> sources, List<string> matched)
  {
    var authorSets = matched
      .Where(s => sources[s].Authors.Count > 0)
      .ToDictionary(s => s, s => sources[s].Authors.Select(NormalizeAuthor).ToHashSet());

    if (authorSets.Count < 2)
    {
      return [];
    }

    var lists = authorSets.Values.ToList();
    var minOverlap = 1.0;
    for (var i = 0; i < lists.Count; i++)
    {
      for (var j = i + 1; j < lists.Count; j++)
      {
        minOverlap = Math.Min(minOverlap, JaccardSimilarity(lists[i], lists[j]));
      }
    }

    if (minOverlap >= 0.7)
    {
      return [];
    }

    return
    [
      new FieldConflictDto
      {
        Field = "authors",
        Status = minOverlap >= 0.5 ? "minorConflict" : "majorConflict",
        Values = matched.ToDictionary(
          s => s,
          s => sources[s].Authors.Count == 0 ? null : string.Join(", ", sources[s].Authors)),
        Note = $"Author overlap is {minOverlap:P0} across sources.",
      },
    ];
  }

  private static Dictionary<string, FieldAuthorityDto> BuildFieldAuthority(
    Dictionary<string, PaperSourceMetadataDto> sources,
    List<string> matched)
  {
    var authority = new Dictionary<string, FieldAuthorityDto>();

    foreach (var field in TrackedFields)
    {
      if (!FieldSourcePriority.TryGetValue(field, out var priority))
      {
        continue;
      }

      var alternatives = new List<string>();
      string? chosenValue = null;
      var chosenFrom = string.Empty;

      foreach (var sourceKey in priority)
      {
        if (!matched.Contains(sourceKey) || !sources.TryGetValue(sourceKey, out var metadata))
        {
          continue;
        }

        var value = GetFieldValue(metadata, field);
        if (string.IsNullOrWhiteSpace(value))
        {
          continue;
        }

        if (string.IsNullOrEmpty(chosenFrom))
        {
          chosenFrom = sourceKey;
          chosenValue = value;
        }
        else
        {
          alternatives.Add($"{sourceKey}: {value}");
        }
      }

      if (!string.IsNullOrEmpty(chosenFrom))
      {
        authority[field] = new FieldAuthorityDto
        {
          Field = field,
          Value = chosenValue,
          ChosenFrom = chosenFrom,
          Alternatives = alternatives,
        };
      }
    }

    return authority;
  }

  private static PaperSourceMetadataDto BuildUnifiedMetadata(Dictionary<string, FieldAuthorityDto> authority, string doi)
  {
    var unified = new PaperSourceMetadataDto
    {
      Source = "unified",
      Found = authority.Count > 0,
      Doi = authority.TryGetValue("doi", out var doiField) ? doiField.Value : doi,
    };

    if (authority.TryGetValue("title", out var title)) unified.Title = title.Value;
    if (authority.TryGetValue("journal", out var journal)) unified.Journal = journal.Value;
    if (authority.TryGetValue("abstract", out var abstractField)) unified.Abstract = abstractField.Value;
    if (authority.TryGetValue("pdfUrl", out var pdf)) unified.PdfUrl = pdf.Value;
    if (authority.TryGetValue("year", out var year) && int.TryParse(year.Value, out var parsedYear)) unified.Year = parsedYear;
    if (authority.TryGetValue("citationCount", out var citations) && int.TryParse(citations.Value, out var parsedCitations)) unified.CitationCount = parsedCitations;
    if (authority.TryGetValue("authors", out var authors) && !string.IsNullOrWhiteSpace(authors.Value))
    {
      unified.Authors = authors.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }
    if (authority.TryGetValue("keywords", out var keywords) && !string.IsNullOrWhiteSpace(keywords.Value))
    {
      unified.Keywords = keywords.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    return unified;
  }

  private static int CalculateCompletenessScore(PaperSourceMetadataDto unified)
  {
    var score = 0;
    foreach (var (field, weight) in CompletenessWeights)
    {
      if (HasFieldValue(unified, field))
      {
        score += weight;
      }
    }

    return score;
  }

  private static int CalculateTrustScore(List<FieldConflictDto> conflicts, int matchedCount, int attemptedCount)
  {
    if (matchedCount == 0)
    {
      return 0;
    }

    var score = 50 + (matchedCount * 10);
    score = Math.Min(score, 80);

    foreach (var conflict in conflicts)
    {
      score -= conflict.Status switch
      {
        "majorConflict" => 15,
        "minorConflict" => 5,
        _ => 0,
      };
    }

    if (matchedCount == attemptedCount && attemptedCount >= 3)
    {
      score += 10;
    }

    return Math.Clamp(score, 0, 100);
  }

  private static string GetConfidenceLevel(int trustScore) => trustScore switch
  {
    >= 80 => "high",
    >= 60 => "medium",
    _ => "low",
  };

  private static List<string> BuildRecommendations(PaperAggregateResultDto result)
  {
    var recommendations = new List<string>();

    if (result.SourcesMatched.Count >= 3 && result.Conflicts.Count == 0)
    {
      recommendations.Add("Metadata is consistent across matched sources.");
    }

    if (result.FieldAuthority.ContainsKey("doi"))
    {
      recommendations.Add("DOI verified and used as the primary identifier.");
    }

    if (result.DataGaps.Any(g => g.Field == "pdfUrl"))
    {
      recommendations.Add("PDF is rarely available from bibliographic APIs; check publisher or arXiv separately.");
    }

    if (result.Conflicts.Any(c => c.Field == "citationCount"))
    {
      recommendations.Add("Citation counts differ between APIs — this is expected due to update timing.");
    }

    if (result.TrustScore < 60)
    {
      recommendations.Add("Review metadata manually before importing or displaying as high-confidence.");
    }

    return recommendations;
  }

  private static bool HasFieldValue(PaperSourceMetadataDto metadata, string field) => field switch
  {
    "doi" => !string.IsNullOrWhiteSpace(metadata.Doi),
    "title" => !string.IsNullOrWhiteSpace(metadata.Title),
    "authors" => metadata.Authors.Count > 0,
    "year" => metadata.Year.HasValue,
    "journal" => !string.IsNullOrWhiteSpace(metadata.Journal),
    "abstract" => !string.IsNullOrWhiteSpace(metadata.Abstract),
    "citationCount" => metadata.CitationCount.HasValue,
    "keywords" => metadata.Keywords.Count > 0,
    "pdfUrl" => !string.IsNullOrWhiteSpace(metadata.PdfUrl),
    _ => false,
  };

  private static string? GetFieldValue(PaperSourceMetadataDto metadata, string field) => field switch
  {
    "doi" => metadata.Doi,
    "title" => metadata.Title,
    "authors" => metadata.Authors.Count == 0 ? null : string.Join(", ", metadata.Authors),
    "year" => metadata.Year?.ToString(),
    "journal" => metadata.Journal,
    "abstract" => metadata.Abstract,
    "citationCount" => metadata.CitationCount?.ToString(),
    "keywords" => metadata.Keywords.Count == 0 ? null : string.Join(", ", metadata.Keywords),
    "pdfUrl" => metadata.PdfUrl,
    _ => null,
  };

  private static string NormalizeAuthor(string author) => author.Trim().ToLowerInvariant();

  private static double JaccardSimilarity(HashSet<string> left, HashSet<string> right)
  {
    if (left.Count == 0 && right.Count == 0)
    {
      return 1;
    }

    var intersection = left.Intersect(right).Count();
    var union = left.Union(right).Count();
    return union == 0 ? 0 : (double)intersection / union;
  }

  private static double TitleSimilarity(string left, string right)
  {
    var a = Tokenize(left);
    var b = Tokenize(right);
    return JaccardSimilarity(a, b);
  }

  private static HashSet<string> Tokenize(string value)
  {
    return value
      .ToLowerInvariant()
      .Split([' ', '-', '_', ',', '.', ':', ';'], StringSplitOptions.RemoveEmptyEntries)
      .ToHashSet();
  }
}
