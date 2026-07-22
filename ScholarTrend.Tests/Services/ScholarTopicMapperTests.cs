using FluentAssertions;
using ScholarTrend.Application.Services.Topics;

namespace ScholarTrend.Tests.Services;

public class ScholarTopicMapperTests
{
    [Fact]
    public void MapToSeededTopics_MapsKnownLabels_ToSeededNames()
    {
        var result = ScholarTopicMapper.MapToSeededTopics(
            ["Machine Learning", "Computer Vision"],
            title: null,
            abstractText: null);

        result.Should().Contain("Artificial Intelligence");
    }

    [Fact]
    public void MapToSeededTopics_FallsBackToTitle_WhenLabelsEmpty()
    {
        var result = ScholarTopicMapper.MapToSeededTopics(
            [],
            title: "Deep learning for cloud microservices",
            abstractText: null);

        result.Should().Contain("Artificial Intelligence");
        result.Should().Contain("Cloud Computing");
    }

    [Fact]
    public void MatchOne_ReturnsNull_ForUnknownLabel()
    {
        ScholarTopicMapper.MatchOne("Quantum Chromodynamics").Should().BeNull();
    }
}
