using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ScholarTrend.Application.DTOs.GapAnalysis;
using ScholarTrend.Application.Interfaces;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;

namespace ScholarTrend.Application.Services;

public class PatternMiningService : IPatternMiningService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PatternMiningService> _logger;

    public PatternMiningService(IUnitOfWork unitOfWork, ILogger<PatternMiningService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PatternMiningResultDto> MinePatternsAsync(int topicId, CancellationToken ct = default)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
            throw new ArgumentException($"Topic {topicId} not found");

        var analyses = await _unitOfWork.PaperAnalyses.GetByTopicIdAsync(topicId);
        return await MineAndPersistAsync(topicId, topic.TopicName, analyses, ct);
    }

    public async Task<PatternMiningResultDto> MinePatternsForPaperIdsAsync(
        int topicId,
        IReadOnlyCollection<int> paperIds,
        CancellationToken ct = default)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
            throw new ArgumentException($"Topic {topicId} not found");

        var analyses = await _unitOfWork.PaperAnalyses.GetByPaperIdsAsync(paperIds);
        return await MineAndPersistAsync(topicId, topic.TopicName, analyses, ct);
    }

    private async Task<PatternMiningResultDto> MineAndPersistAsync(
        int topicId,
        string topicName,
        List<PaperAnalysis> analyses,
        CancellationToken ct)
    {
        var methods = MineMethods(analyses);
        var datasets = MineDatasets(analyses);
        var limitations = MineLimitations(analyses);

        var methodPatterns = ConvertToMethodPatterns(topicId, methods);
        var datasetPatterns = ConvertToDatasetPatterns(topicId, datasets);
        var limitationPatterns = ConvertToLimitationPatterns(topicId, limitations);

        await _unitOfWork.Patterns.UpsertMethodPatternsAsync(methodPatterns);
        await _unitOfWork.Patterns.UpsertDatasetPatternsAsync(datasetPatterns);
        await _unitOfWork.Patterns.UpsertLimitationPatternsAsync(limitationPatterns);
        await _unitOfWork.Context.SaveChangesAsync(ct);

        return new PatternMiningResultDto
        {
            TopicId = topicId,
            TopicName = topicName,
            Methods = methods.Select(m => new MethodPatternDto
            {
                MethodName = m.Key,
                PaperCount = m.Value,
                Year = DateTime.UtcNow.Year,
                GrowthRate = 0,
                Trend = "stable"
            }).OrderByDescending(m => m.PaperCount).ToList(),
            Datasets = datasets.Select(d => new DatasetPatternDto
            {
                DatasetName = d.Key,
                PaperCount = d.Value,
                Year = DateTime.UtcNow.Year,
                GrowthRate = 0,
                Trend = "stable"
            }).OrderByDescending(d => d.PaperCount).ToList(),
            Limitations = limitations.Select(l => new LimitationPatternDto
            {
                LimitationText = l.Key,
                PaperCount = l.Value,
                Year = DateTime.UtcNow.Year,
                GrowthRate = 0,
                Trend = "stable"
            }).OrderByDescending(l => l.PaperCount).ToList(),
            MinedAt = DateTime.UtcNow
        };
    }

    public async Task<PatternMiningResultDto> GetStoredPatternsAsync(int topicId, CancellationToken ct = default)
    {
        var topic = await _unitOfWork.Topics.GetByIdAsync(topicId);
        if (topic == null)
            throw new ArgumentException($"Topic {topicId} not found");

        var methodPatterns = await _unitOfWork.Patterns.GetMethodPatternsAsync(topicId);
        var datasetPatterns = await _unitOfWork.Patterns.GetDatasetPatternsAsync(topicId);
        var limitationPatterns = await _unitOfWork.Patterns.GetLimitationPatternsAsync(topicId);

        return new PatternMiningResultDto
        {
            TopicId = topicId,
            TopicName = topic.TopicName,
            Methods = methodPatterns
                .GroupBy(p => p.MethodName)
                .Select(g => new MethodPatternDto
                {
                    MethodName = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(m => m.PaperCount)
                .ToList(),
            Datasets = datasetPatterns
                .GroupBy(p => p.DatasetName)
                .Select(g => new DatasetPatternDto
                {
                    DatasetName = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(d => d.PaperCount)
                .ToList(),
            Limitations = limitationPatterns
                .GroupBy(p => p.LimitationText)
                .Select(g => new LimitationPatternDto
                {
                    LimitationText = g.Key,
                    PaperCount = g.Sum(p => p.PaperCount),
                    Year = g.Max(p => p.Year),
                    GrowthRate = 0,
                    Trend = "stable"
                })
                .OrderByDescending(l => l.PaperCount)
                .ToList(),
            MinedAt = DateTime.UtcNow
        };
    }

    public async Task<Dictionary<string, int>> GetMethodFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null)
    {
        var patterns = await _unitOfWork.Patterns.GetMethodPatternsAsync(topicId, yearFrom, yearTo);
        return patterns.GroupBy(p => p.MethodName)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaperCount));
    }

    public async Task<Dictionary<string, int>> GetDatasetFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null)
    {
        var patterns = await _unitOfWork.Patterns.GetDatasetPatternsAsync(topicId, yearFrom, yearTo);
        return patterns.GroupBy(p => p.DatasetName)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaperCount));
    }

    public async Task<Dictionary<string, int>> GetLimitationFrequencyAsync(int topicId, int? yearFrom = null, int? yearTo = null)
    {
        var patterns = await _unitOfWork.Patterns.GetLimitationPatternsAsync(topicId, yearFrom, yearTo);
        return patterns.GroupBy(p => p.LimitationText)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaperCount));
    }

    private Dictionary<string, int> MineMethods(List<PaperAnalysis> analyses)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var analysis in analyses)
        {
            var methods = DeserializeList(analysis.MethodsJson);
            foreach (var method in methods)
            {
                var normalized = NormalizeMethod(method);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result[normalized] = result.GetValueOrDefault(normalized, 0) + 1;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(analysis.Method))
            {
                var normalized = NormalizeMethod(analysis.Method);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result[normalized] = result.GetValueOrDefault(normalized, 0) + 1;
                }
            }
        }

        return result;
    }

    private Dictionary<string, int> MineDatasets(List<PaperAnalysis> analyses)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var analysis in analyses)
        {
            var datasets = DeserializeList(analysis.DatasetsJson);
            foreach (var dataset in datasets)
            {
                var normalized = NormalizeDataset(dataset);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result[normalized] = result.GetValueOrDefault(normalized, 0) + 1;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(analysis.Dataset))
            {
                var normalized = NormalizeDataset(analysis.Dataset);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result[normalized] = result.GetValueOrDefault(normalized, 0) + 1;
                }
            }
        }

        return result;
    }

    private Dictionary<string, int> MineLimitations(List<PaperAnalysis> analyses)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var analysis in analyses)
        {
            var limitations = DeserializeList(analysis.LimitationsJson);
            foreach (var limitation in limitations)
            {
                var normalized = NormalizeLimitation(limitation);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result[normalized] = result.GetValueOrDefault(normalized, 0) + 1;
                }
            }
        }

        return result;
    }

    private string NormalizeMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method)) return "";
        var normalized = method.Trim().ToLowerInvariant();
        
        var methodMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["transformer"] = "Transformer",
            ["transformers"] = "Transformer",
            ["bert"] = "BERT",
            ["gpt"] = "GPT",
            ["llm"] = "LLM",
            ["large language model"] = "LLM",
            ["cnn"] = "CNN",
            ["rnn"] = "RNN",
            ["lstm"] = "LSTM",
            ["gru"] = "GRU",
            ["gan"] = "GAN",
            ["vae"] = "VAE",
            ["diffusion"] = "Diffusion Model",
            ["rag"] = "RAG",
            ["retrieval augmented"] = "RAG",
            ["attention"] = "Attention Mechanism",
            ["self attention"] = "Self-Attention",
            ["reinforcement learning"] = "RL",
            ["rl"] = "RL",
            ["transfer learning"] = "Transfer Learning",
            ["fine tuning"] = "Fine-tuning",
            ["few shot"] = "Few-shot Learning",
            ["zero shot"] = "Zero-shot Learning",
            ["graph neural network"] = "GNN",
            ["gnn"] = "GNN"
        };

        return methodMappings.TryGetValue(normalized, out var mapped) ? mapped : method.Trim();
    }

    private string NormalizeDataset(string dataset)
    {
        if (string.IsNullOrWhiteSpace(dataset)) return "";
        return dataset.Trim();
    }

    private string NormalizeLimitation(string limitation)
    {
        if (string.IsNullOrWhiteSpace(limitation)) return "";
        
        var lower = limitation.ToLowerInvariant();
        if (lower.Contains("dataset") || lower.Contains("data"))
            return "Need Larger/Better Dataset";
        if (lower.Contains("explain") || lower.Contains("interpret"))
            return "Need Explainability";
        if (lower.Contains("real") || lower.Contains("time"))
            return "Need Real-time Processing";
        if (lower.Contains("generaliz") || lower.Contains("robust"))
            return "Need Better Generalization";
        if (lower.Contains("bias") || lower.Contains("fair"))
            return "Need Fairness/Bias Reduction";
        if (lower.Contains("compute") || lower.Contains("resource"))
            return "Computational Resource Limitations";
        
        return limitation.Length > 100 ? limitation.Substring(0, 100) + "..." : limitation;
    }

    private List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch { return []; }
    }

    private List<MethodPattern> ConvertToMethodPatterns(int topicId, Dictionary<string, int> methods)
    {
        var year = DateTime.UtcNow.Year;
        return methods.Select(m => new MethodPattern
        {
            TopicId = topicId,
            MethodName = m.Key,
            PaperCount = m.Value,
            Year = year,
            MinedAt = DateTime.UtcNow
        }).ToList();
    }

    private List<DatasetPattern> ConvertToDatasetPatterns(int topicId, Dictionary<string, int> datasets)
    {
        var year = DateTime.UtcNow.Year;
        return datasets.Select(d => new DatasetPattern
        {
            TopicId = topicId,
            DatasetName = d.Key,
            PaperCount = d.Value,
            Year = year,
            MinedAt = DateTime.UtcNow
        }).ToList();
    }

    private List<LimitationPattern> ConvertToLimitationPatterns(int topicId, Dictionary<string, int> limitations)
    {
        var year = DateTime.UtcNow.Year;
        return limitations.Select(l => new LimitationPattern
        {
            TopicId = topicId,
            LimitationText = l.Key,
            PaperCount = l.Value,
            Year = year,
            MinedAt = DateTime.UtcNow
        }).ToList();
    }
}
