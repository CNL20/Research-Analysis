using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class TrendAnalysisService : ITrendAnalysisService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TrendAnalysisService> _logger;

    public TrendAnalysisService(IUnitOfWork unitOfWork, ILogger<TrendAnalysisService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<GapTimelineDto> GetGapTimelineAsync(int topicId)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        var timelines = await _unitOfWork.GapTimelines.GetByTopicIdAsync(topicId);

        return new GapTimelineDto
        {
            TopicId = topicId,
            TopicName = topic?.TopicName ?? "",
            Timeline = timelines.Select(t => new GapTimelineEntryDto
            {
                Year = t.Year,
                GapType = t.GapType,
                GapTitle = t.GapTitle,
                PaperCount = t.PaperCount,
                IsResolved = t.IsResolved,
                Trend = t.Trend,
                GrowthRate = t.GrowthRate
            }).OrderBy(t => t.Year).ToList()
        };
    }

    public async Task<TrendAnalysisResultDto> AnalyzeMethodTrendAsync(int topicId, string methodName)
    {
        var patterns = await _unitOfWork.Patterns.GetMethodPatternsAsync(topicId);
        var methodPatterns = patterns.Where(p => 
            p.MethodName.Equals(methodName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dataPoints = methodPatterns
            .GroupBy(p => p.Year)
            .Select(g => new TrendDataPointDto
            {
                Year = g.Key,
                PaperCount = g.Sum(p => p.PaperCount),
                GrowthRate = CalculateGrowthRate(g.Key, g.Sum(p => p.PaperCount), methodPatterns)
            })
            .OrderBy(d => d.Year)
            .ToList();

        return new TrendAnalysisResultDto
        {
            TopicId = topicId,
            TargetType = "Method",
            TargetName = methodName,
            DataPoints = dataPoints,
            OverallTrend = DetermineTrend(dataPoints),
            GrowthRate = CalculateOverallGrowthRate(dataPoints),
            Status = DetermineStatus(dataPoints)
        };
    }

    public async Task<TrendAnalysisResultDto> AnalyzeDatasetTrendAsync(int topicId, string datasetName)
    {
        var patterns = await _unitOfWork.Patterns.GetDatasetPatternsAsync(topicId);
        var datasetPatterns = patterns.Where(p => 
            p.DatasetName.Equals(datasetName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dataPoints = datasetPatterns
            .GroupBy(p => p.Year)
            .Select(g => new TrendDataPointDto
            {
                Year = g.Key,
                PaperCount = g.Sum(p => p.PaperCount),
                GrowthRate = CalculateGrowthRate(g.Key, g.Sum(p => p.PaperCount), datasetPatterns)
            })
            .OrderBy(d => d.Year)
            .ToList();

        return new TrendAnalysisResultDto
        {
            TopicId = topicId,
            TargetType = "Dataset",
            TargetName = datasetName,
            DataPoints = dataPoints,
            OverallTrend = DetermineTrend(dataPoints),
            GrowthRate = CalculateOverallGrowthRate(dataPoints),
            Status = DetermineStatus(dataPoints)
        };
    }

    public async Task<TrendAnalysisResultDto> AnalyzeLimitationTrendAsync(int topicId, string limitationKeyword)
    {
        var patterns = await _unitOfWork.Patterns.GetLimitationPatternsAsync(topicId);
        var limitationPatterns = patterns.Where(p => 
            p.LimitationText.Contains(limitationKeyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var dataPoints = limitationPatterns
            .GroupBy(p => p.Year)
            .Select(g => new TrendDataPointDto
            {
                Year = g.Key,
                PaperCount = g.Sum(p => p.PaperCount),
                GrowthRate = CalculateGrowthRate(g.Key, g.Sum(p => p.PaperCount), limitationPatterns)
            })
            .OrderBy(d => d.Year)
            .ToList();

        return new TrendAnalysisResultDto
        {
            TopicId = topicId,
            TargetType = "Limitation",
            TargetName = limitationKeyword,
            DataPoints = dataPoints,
            OverallTrend = DetermineTrend(dataPoints),
            GrowthRate = CalculateOverallGrowthRate(dataPoints),
            Status = DetermineStatus(dataPoints)
        };
    }

    public async Task<List<TrendAnalysisResultDto>> GetTopMethodTrendsAsync(int topicId, int top = 10)
    {
        var patterns = await _unitOfWork.Patterns.GetMethodPatternsAsync(topicId);
        var topMethods = patterns
            .GroupBy(p => p.MethodName)
            .Select(g => new { Method = g.Key, Total = g.Sum(p => p.PaperCount), Patterns = g.ToList() })
            .OrderByDescending(x => x.Total)
            .Take(top)
            .ToList();

        var results = new List<TrendAnalysisResultDto>();
        foreach (var m in topMethods)
        {
            var dataPoints = m.Patterns
                .GroupBy(p => p.Year)
                .Select(g => new TrendDataPointDto
                {
                    Year = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    GrowthRate = 0
                })
                .OrderBy(d => d.Year)
                .ToList();

            results.Add(new TrendAnalysisResultDto
            {
                TopicId = topicId,
                TargetType = "Method",
                TargetName = m.Method,
                DataPoints = dataPoints,
                OverallTrend = DetermineTrend(dataPoints),
                GrowthRate = CalculateOverallGrowthRate(dataPoints),
                Status = DetermineStatus(dataPoints)
            });
        }

        return results;
    }

    private double CalculateGrowthRate(int year, int paperCount, List<MethodPattern> allPatterns)
    {
        var previousYear = year - 1;
        var previousCount = allPatterns.Where(p => p.Year == previousYear).Sum(p => p.PaperCount);
        if (previousCount == 0) return 0;
        return (paperCount - previousCount) * 100.0 / previousCount;
    }

    private double CalculateGrowthRate(int year, int paperCount, List<DatasetPattern> allPatterns)
    {
        var previousYear = year - 1;
        var previousCount = allPatterns.Where(p => p.Year == previousYear).Sum(p => p.PaperCount);
        if (previousCount == 0) return 0;
        return (paperCount - previousCount) * 100.0 / previousCount;
    }

    private double CalculateGrowthRate(int year, int paperCount, List<LimitationPattern> allPatterns)
    {
        var previousYear = year - 1;
        var previousCount = allPatterns.Where(p => p.Year == previousYear).Sum(p => p.PaperCount);
        if (previousCount == 0) return 0;
        return (paperCount - previousCount) * 100.0 / previousCount;
    }

    private string DetermineTrend(List<TrendDataPointDto> dataPoints)
    {
        if (dataPoints.Count < 2) return GapTrends.Stable;
        
        var recent = dataPoints.TakeLast(3).ToList();
        var older = dataPoints.Take(dataPoints.Count - 3).ToList();
        
        if (!recent.Any() || !older.Any()) return GapTrends.Stable;
        
        var recentAvg = recent.Average(d => d.PaperCount);
        var olderAvg = older.Average(d => d.PaperCount);
        
        if (recentAvg > olderAvg * 1.2) return GapTrends.Increasing;
        if (recentAvg < olderAvg * 0.8) return GapTrends.Decreasing;
        return GapTrends.Stable;
    }

    private double CalculateOverallGrowthRate(List<TrendDataPointDto> dataPoints)
    {
        if (dataPoints.Count < 2) return 0;
        var first = dataPoints.First().PaperCount;
        var last = dataPoints.Last().PaperCount;
        if (first == 0) return 0;
        return (last - first) * 100.0 / first;
    }

    private string DetermineStatus(List<TrendDataPointDto> dataPoints)
    {
        if (!dataPoints.Any()) return GapTrends.Stable;
        
        var trend = DetermineTrend(dataPoints);
        if (trend == GapTrends.Increasing) return "Emerging";
        if (trend == GapTrends.Decreasing) return "Declining";
        
        var total = dataPoints.Sum(d => d.PaperCount);
        return total > 50 ? "Established" : "Niche";
    }
}
