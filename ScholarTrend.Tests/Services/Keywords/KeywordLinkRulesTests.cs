using FluentAssertions;
using ScholarTrend.Application.Services.Keywords;
using Xunit;

namespace ScholarTrend.Tests.Services.Keywords;

public class KeywordLinkRulesTests
{
    [Fact]
    public void PrepareKeywordNames_Filters_Stoplist_And_Maps_Seed()
    {
        var result = KeywordLinkRules.PrepareKeywordNames([
            "Computer science",
            "machine learning",
            "  deep learning  ",
            "Transformer",
            "machine learning"
        ]);

        result.Should().BeEquivalentTo([
            "Machine Learning",
            "Deep Learning",
            "Transformer"
        ]);
    }

    [Fact]
    public void PrepareKeywordNames_Maps_Nlp_Alias()
    {
        var result = KeywordLinkRules.PrepareKeywordNames(["NLP", "IoT"]);

        result.Should().BeEquivalentTo([
            "Natural Language Processing",
            "Internet of Things"
        ]);
    }

    [Fact]
    public void IsBlocked_Rejects_Short_And_Generic_Terms()
    {
        KeywordLinkRules.IsBlocked("ai").Should().BeTrue();
        KeywordLinkRules.IsBlocked("research").Should().BeTrue();
    }

    [Theory]
    [InlineData(10, 50, 120, 12.5)]
    [InlineData(0, 100, 0, 10.0)]
    public void CalculateTrendingScore_Matches_Seed_Formula(
        int paperCount, double growthRate, int citationCount, double expected)
    {
        KeywordTrendCalculator.CalculateTrendingScore(paperCount, growthRate, citationCount)
            .Should().Be(expected);
    }

    [Fact]
    public void MatchSeedsFromText_Matches_Alias_And_Phrases()
    {
        var result = KeywordLinkRules.MatchSeedsFromText(
            "Advanced Intelligent Computing Technology and AI-assisted robotics");

        result.Should().Contain("Artificial Intelligence");
    }
}
