using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Domain.Entities;
using System.Text.Json;

namespace ScholarTrend.Application.Services;

public class TopicInsightService : ITopicInsightService
{
    private readonly IUnitOfWork _unitOfWork;

    public TopicInsightService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TopicInsightDashboardDto> GetTopicInsightDashboardAsync(int topicId)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
            throw new InvalidOperationException("Topic not found.");

        // We fetch the latest insight for this topic
        var latestInsight = await _unitOfWork.Context.Set<TopicInsight>()
            .Include(t => t.Evidences)
            .Where(t => t.TopicId == topicId)
            .OrderByDescending(t => t.Year)
            .FirstOrDefaultAsync();

        if (latestInsight == null)
        {
            // Return empty dashboard if AI hasn't analyzed yet
            return new TopicInsightDashboardDto
            {
                TopicId = topicId,
                TopicName = topic.TopicName,
                LastAnalyzedAt = DateTime.UtcNow
            };
        }

        var topMethods = string.IsNullOrWhiteSpace(latestInsight.TopMethodsJson) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(latestInsight.TopMethodsJson) ?? new List<string>();
            
        var topDatasets = string.IsNullOrWhiteSpace(latestInsight.TopDatasetsJson) 
            ? new List<string>() 
            : JsonSerializer.Deserialize<List<string>>(latestInsight.TopDatasetsJson) ?? new List<string>();

        var rawOpportunities = string.IsNullOrWhiteSpace(latestInsight.FutureDirectionsJson)
            ? new List<AiOpportunityDto>()
            : JsonSerializer.Deserialize<List<AiOpportunityDto>>(latestInsight.FutureDirectionsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<AiOpportunityDto>();

        // Map opportunities to DTO
        var opportunities = rawOpportunities.Select(ro => new ResearchOpportunityDto
        {
            Title = ro.Title,
            Description = ro.Description,
            // Find evidences linked to this insight
            Evidences = latestInsight.Evidences.Select(e => new EvidenceDto
            {
                PaperId = e.PaperId,
                Excerpt = e.Excerpt
            }).ToList()
        }).ToList();

        // Get historical insights for timeline
        var historicalInsights = await _unitOfWork.Context.Set<TopicInsight>()
            .Where(t => t.TopicId == topicId)
            .OrderBy(t => t.Year)
            .ToListAsync();

        var timeline = historicalInsights.Select(hi => new TimelineDto
        {
            Year = hi.Year,
            Achievement = hi.Achievement,
            Summary = hi.Summary,
            PaperCount = hi.PaperCountAtGeneration
        }).ToList();

        return new TopicInsightDashboardDto
        {
            TopicId = topicId,
            TopicName = topic.TopicName,
            LastAnalyzedAt = latestInsight.CreatedAt,
            TopMethods = topMethods,
            TopDatasets = topDatasets,
            Timeline = timeline,
            Opportunities = opportunities
        };
    }
}
