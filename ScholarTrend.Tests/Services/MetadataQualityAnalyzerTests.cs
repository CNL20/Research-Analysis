using ScholarTrend.Application.DTOs.Aggregation;
using ScholarTrend.Application.Services.Aggregation;

namespace ScholarTrend.Tests.Services;

public class MetadataQualityAnalyzerTests
{
    [Fact]
    public void Analyze_ShouldBuildUnifiedMetadata_WithSourcePriority()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["crossref"] = new()
            {
                Source = "crossref",
                Found = true,
                Doi = "10.1000/test",
                Title = "Crossref Title",
                Year = 2024,
                Journal = "Nature",
                Authors = ["Alice"],
            },
            ["openalex"] = new()
            {
                Source = "openalex",
                Found = true,
                Doi = "10.1000/test",
                Title = "Crossref Title",
                Year = 2024,
                CitationCount = 120,
                Authors = ["Alice", "Bob"],
                Keywords = ["AI", "Health"],
            },
            ["semantic_scholar"] = new()
            {
                Source = "semantic_scholar",
                Found = true,
                Doi = "10.1000/test",
                Title = "Crossref Title",
                Year = 2024,
                Abstract = "An abstract about AI.",
                CitationCount = 115,
                Authors = ["Alice", "Bob"],
            },
            ["internal"] = new()
            {
                Source = "internal",
                Found = false,
            },
        };

        var result = MetadataQualityAnalyzer.Analyze("10.1000/test", sources);

        Assert.Equal(3, result.SourcesMatched.Count);
        Assert.Equal("crossref", result.FieldAuthority["doi"].ChosenFrom);
        Assert.Equal("semantic_scholar", result.FieldAuthority["abstract"].ChosenFrom);
        Assert.Equal("An abstract about AI.", result.UnifiedMetadata.Abstract);
        Assert.True(result.CompletenessScore >= 80);
        Assert.True(result.TrustScore >= 60);
    }

    [Fact]
    public void Analyze_ShouldDetectMajorYearConflict()
    {
        var sources = new Dictionary<string, PaperSourceMetadataDto>
        {
            ["openalex"] = new() { Source = "openalex", Found = true, Doi = "10.1000/conflict", Title = "Same", Year = 2024 },
            ["crossref"] = new() { Source = "crossref", Found = true, Doi = "10.1000/conflict", Title = "Same", Year = 2020 },
        };

        var result = MetadataQualityAnalyzer.Analyze("10.1000/conflict", sources);

        Assert.Contains(result.Conflicts, c => c.Field == "year" && c.Status == "majorConflict");
    }
}
