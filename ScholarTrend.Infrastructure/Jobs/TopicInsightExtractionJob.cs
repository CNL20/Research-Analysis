using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;
using System.Text.Json;

namespace ScholarTrend.Infrastructure.Jobs;

public class TopicInsightExtractionJob
{
    private readonly ScholarTrendDbContext _dbContext;
    private readonly IAiExtractionService _aiExtractionService;
    private readonly ILogger<TopicInsightExtractionJob> _logger;

    public TopicInsightExtractionJob(
        ScholarTrendDbContext dbContext,
        IAiExtractionService aiExtractionService,
        ILogger<TopicInsightExtractionJob> logger)
    {
        _dbContext = dbContext;
        _aiExtractionService = aiExtractionService;
        _logger = logger;
    }

    public async Task RunExtractionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting TopicInsightExtractionJob...");

        // Find papers that have a Topic (via PaperTopic) but do NOT have a PaperTopicExtraction record yet.
        var pendingPapers = await _dbContext.ResearchPapers
            .Include(p => p.PaperTopics)
            .Where(p => p.PaperTopics.Any() && !string.IsNullOrWhiteSpace(p.Abstract))
            .Where(p => !_dbContext.PaperTopicExtractions.Any(e => e.PaperId == p.Id))
            .Take(10) // Process in batches to respect API rate limits
            .ToListAsync(cancellationToken);

        if (!pendingPapers.Any())
        {
            _logger.LogInformation("No pending papers to extract.");
            return;
        }

        foreach (var paper in pendingPapers)
        {
            try
            {
                _logger.LogInformation($"Extracting insights for Paper ID: {paper.Id}");
                
                var extractedData = await _aiExtractionService.ExtractFromAbstractAsync(paper.Abstract, cancellationToken);
                
                if (extractedData != null)
                {
                    // A paper can belong to multiple topics, we save the extraction for each associated topic
                    foreach (var pt in paper.PaperTopics)
                    {
                        var extractionRecord = new PaperTopicExtraction
                        {
                            PaperId = paper.Id,
                            TopicId = pt.TopicId,
                            MethodsJson = JsonSerializer.Serialize(extractedData.Methods),
                            DatasetsJson = JsonSerializer.Serialize(extractedData.Datasets),
                            LimitationsJson = JsonSerializer.Serialize(extractedData.Limitations),
                            FutureWorkJson = JsonSerializer.Serialize(extractedData.FutureWork),
                            ExtractedAt = DateTime.UtcNow
                        };
                        _dbContext.PaperTopicExtractions.Add(extractionRecord);
                    }
                }
                
                // Save after each paper to not lose progress if rate limited
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                // Add a small delay to avoid hitting rate limits (e.g. Gemini Free allows 15 RPM)
                await Task.Delay(4000, cancellationToken); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting data for paper {paper.Id}");
            }
        }
        
        _logger.LogInformation("Finished TopicInsightExtractionJob batch.");
    }
}
