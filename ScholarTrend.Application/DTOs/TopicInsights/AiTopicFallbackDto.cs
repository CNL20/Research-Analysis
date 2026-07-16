using System.Collections.Generic;

namespace ScholarTrend.Application.DTOs.TopicInsights;

public class AiTopicFallbackDto
{
    public List<string> Methods { get; set; } = new();
    public List<string> Datasets { get; set; } = new();
    public List<AiOpportunityDto> Opportunities { get; set; } = new();
}
