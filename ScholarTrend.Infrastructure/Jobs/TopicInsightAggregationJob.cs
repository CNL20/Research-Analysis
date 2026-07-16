using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;
using System.Text.Json;

namespace ScholarTrend.Infrastructure.Jobs;

public class TopicInsightAggregationJob
{
    private readonly ScholarTrendDbContext _dbContext;
    private readonly IAiExtractionService _aiExtractionService;
    private readonly ILogger<TopicInsightAggregationJob> _logger;

    public TopicInsightAggregationJob(
        ScholarTrendDbContext dbContext,
        IAiExtractionService aiExtractionService,
        ILogger<TopicInsightAggregationJob> logger)
    {
        _dbContext = dbContext;
        _aiExtractionService = aiExtractionService;
        _logger = logger;
    }

    public async Task RunAggregationAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting TopicInsightAggregationJob...");

        // We find all topics that have at least one extraction, but no topic insight created for the current year
        var currentYear = DateTime.UtcNow.Year;
        
        var topicsToProcess = await _dbContext.ResearchTopics
            .Where(t => _dbContext.PaperTopicExtractions.Any(e => e.TopicId == t.Id))
            .Where(t => !_dbContext.TopicInsights.Any(ti => ti.TopicId == t.Id && ti.Year == currentYear))
            .ToListAsync(cancellationToken);

        if (!topicsToProcess.Any())
        {
            _logger.LogInformation("No topics require aggregation right now.");
            return;
        }

        foreach (var topic in topicsToProcess)
        {
            _logger.LogInformation($"Aggregating insights for Topic: {topic.TopicName}");
            
            // 1. Get all extractions for this topic
            var extractions = await _dbContext.PaperTopicExtractions
                .Where(e => e.TopicId == topic.Id)
                .Select(e => new { e.PaperId, e.MethodsJson, e.DatasetsJson, e.FutureWorkJson })
                .ToListAsync(cancellationToken);

            if (!extractions.Any()) continue;

            // 2. Aggregate Methods & Datasets (Phase 3 - Part 1)
            var allMethods = new Dictionary<string, int>();
            var allDatasets = new Dictionary<string, int>();
            var allFutureWorks = new List<string>();
            var paperIdsMapping = new List<int>(); // To track which future work belongs to which paper

            foreach (var ext in extractions)
            {
                if (!string.IsNullOrWhiteSpace(ext.MethodsJson))
                {
                    var methods = JsonSerializer.Deserialize<List<string>>(ext.MethodsJson);
                    if (methods != null)
                    {
                        foreach (var m in methods)
                        {
                            if (allMethods.ContainsKey(m)) allMethods[m]++;
                            else allMethods[m] = 1;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(ext.DatasetsJson))
                {
                    var datasets = JsonSerializer.Deserialize<List<string>>(ext.DatasetsJson);
                    if (datasets != null)
                    {
                        foreach (var d in datasets)
                        {
                            if (allDatasets.ContainsKey(d)) allDatasets[d]++;
                            else allDatasets[d] = 1;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(ext.FutureWorkJson))
                {
                    var fws = JsonSerializer.Deserialize<List<string>>(ext.FutureWorkJson);
                    if (fws != null && fws.Any())
                    {
                        foreach (var fw in fws)
                        {
                            allFutureWorks.Add(fw);
                            paperIdsMapping.Add(ext.PaperId);
                        }
                    }
                }
            }

            var topMethods = allMethods.OrderByDescending(x => x.Value).Take(5).Select(x => x.Key).ToList();
            var topDatasets = allDatasets.OrderByDescending(x => x.Value).Take(5).Select(x => x.Key).ToList();

            // 3. Summarize Opportunities using AI (Phase 3 - Part 2)
            // Limit to 15 future works to avoid exceeding free API quotas
            var limitedFutureWorks = allFutureWorks.Take(15).ToList();
            var aiOpportunities = await _aiExtractionService.SummarizeOpportunitiesAsync(topic.TopicName, limitedFutureWorks, cancellationToken);

            // 3b. AI Fallback: Generate insights directly if they are missing
            bool needMethods = !topMethods.Any();
            bool needDatasets = !topDatasets.Any();
            bool needOpportunities = aiOpportunities == null || !aiOpportunities.Any();

            if (needMethods || needDatasets || needOpportunities)
            {
                _logger.LogInformation("Falling back to AI to generate missing insights for: {Topic}", topic.TopicName);
                var fallback = await _aiExtractionService.GenerateFallbackInsightsAsync(topic.TopicName, needMethods, needDatasets, needOpportunities, cancellationToken);
                
                if (fallback != null)
                {
                    if (needMethods && fallback.Methods != null) topMethods = fallback.Methods;
                    if (needDatasets && fallback.Datasets != null) topDatasets = fallback.Datasets;
                    if (needOpportunities && fallback.Opportunities != null) aiOpportunities = fallback.Opportunities;
                }
            }
            
            // 4. Save to Database
            var newInsight = new TopicInsight
            {
                TopicId = topic.Id,
                Year = currentYear,
                Achievement = "System is tracking emerging methodologies and datasets.",
                Summary = "Auto-generated aggregation.",
                FutureDirectionsJson = JsonSerializer.Serialize(aiOpportunities.Select(o => new { Title = o.Title, Description = o.Description })),
                TopMethodsJson = JsonSerializer.Serialize(topMethods),
                TopDatasetsJson = JsonSerializer.Serialize(topDatasets),
                PaperCountAtGeneration = paperIdsMapping.Distinct().Count(),
                CreatedAt = DateTime.UtcNow
            };
            
            _dbContext.TopicInsights.Add(newInsight);
            await _dbContext.SaveChangesAsync(cancellationToken); // Save to get newInsight.Id

            // 5. Create Evidences
            if (aiOpportunities != null && aiOpportunities.Any())
            {
                foreach (var opp in aiOpportunities)
                {
                    foreach (var index in opp.SourceIndices)
                    {
                        if (index >= 0 && index < paperIdsMapping.Count)
                        {
                            var evidence = new TopicInsightEvidence
                            {
                                TopicInsightId = newInsight.Id,
                                PaperId = paperIdsMapping[index],
                                Excerpt = allFutureWorks[index],
                                EvidenceType = "Opportunity"
                            };
                            _dbContext.TopicInsightEvidences.Add(evidence);
                        }
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            
            // Small delay for stability
            _logger.LogInformation("Waiting 1 second before processing next topic...");
            await Task.Delay(1000, cancellationToken);
        }
        
        _logger.LogInformation("Finished TopicInsightAggregationJob batch.");
    }
}
