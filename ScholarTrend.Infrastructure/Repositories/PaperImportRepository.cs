using Microsoft.EntityFrameworkCore;
using ScholarTrend.Application.Interfaces.External;
using ScholarTrend.Application.Interfaces.Repositories;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Domain.Enums;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Repositories;

public class PaperImportRepository : IPaperImportRepository
{
    private readonly ScholarTrendDbContext _context;

    public PaperImportRepository(ScholarTrendDbContext context)
    {
        _context = context;
    }

    public async Task<ResearchPaperImportResult> ImportAsync(ExternalPaperDto external, int? journalId)
    {
        var existing = await _context.ResearchPapers
            .FirstOrDefaultAsync(p => p.ExternalId == external.ExternalId && p.ExternalSource == external.Source);

        if (existing != null)
        {
            existing.CitationCount = external.CitationCount;
            if (!string.IsNullOrEmpty(external.PdfUrl))
            {
                existing.PdfUrl = external.PdfUrl;
            }
            existing.UpdatedAt = DateTime.UtcNow;
            existing.Status = PaperStatus.Updated;
            _context.ResearchPapers.Update(existing);
            await _context.SaveChangesAsync();
            return new ResearchPaperImportResult { PaperId = existing.Id, IsNew = false };
        }

        var paper = new ResearchPaper
        {
            Title = external.Title,
            Abstract = external.Abstract,
            PublicationYear = external.Year,
            PublicationDate = external.Year.HasValue ? new DateTime(external.Year.Value, 6, 1) : null,
            CitationCount = external.CitationCount,
            Doi = external.Doi,
            Url = external.Url,
            PdfUrl = external.PdfUrl,
            ExternalId = external.ExternalId,
            ExternalSource = external.Source,
            JournalId = journalId,
            Status = PaperStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        await _context.ResearchPapers.AddAsync(paper);
        await _context.SaveChangesAsync();

        await LinkAuthorsAsync(paper.Id, external.AuthorNames);
        await LinkKeywordsAndTopicAsync(paper.Id, external);

        return new ResearchPaperImportResult { PaperId = paper.Id, IsNew = true };
    }

    private async Task LinkAuthorsAsync(int paperId, List<string> authorNames)
    {
        var order = 1;
        foreach (var name in authorNames.Take(5))
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var author = await _context.Authors.FirstOrDefaultAsync(a => a.Name == name);
            if (author == null)
            {
                author = new Author { Name = name };
                await _context.Authors.AddAsync(author);
                await _context.SaveChangesAsync();
            }

            var exists = await _context.PaperAuthors.AnyAsync(pa => pa.PaperId == paperId && pa.AuthorId == author.Id);
            if (!exists)
            {
                await _context.PaperAuthors.AddAsync(new PaperAuthor
                {
                    PaperId = paperId,
                    AuthorId = author.Id,
                    AuthorOrder = order++
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task LinkKeywordsAndTopicAsync(int paperId, ExternalPaperDto external)
    {
        var keywords = await _context.Keywords.ToListAsync();
        var titleLower = external.Title?.ToLowerInvariant() ?? "";
        var abstractLower = external.Abstract?.ToLowerInvariant() ?? "";
        var text = $"{titleLower} {abstractLower}";
        var matched = keywords.Where(k => text.Contains(k.Name.ToLowerInvariant())).Take(3).ToList();

        foreach (var keyword in matched)
        {
            var exists = await _context.PaperKeywords.AnyAsync(pk => pk.PaperId == paperId && pk.KeywordId == keyword.Id);
            if (!exists)
            {
                await _context.PaperKeywords.AddAsync(new PaperKeyword { PaperId = paperId, KeywordId = keyword.Id });
            }
        }

        // Match topic based on keywords in title/abstract
        // Check if any keyword matches a topic name
        // EF.Functions.ILike: Postgres-only, case-insensitive, fully translatable to SQL
        var topic = matched.Count > 0
            ? await _context.ResearchTopics.FirstOrDefaultAsync(t =>
                matched.Any(k =>
                    EF.Functions.ILike(t.TopicName, $"%{k.Name}%") ||
                    EF.Functions.ILike(k.Name, $"%{t.TopicName}%")))
            : null;

        // If no topic matched via keywords, use ML/AI keywords to determine topic
        if (topic == null)
        {
            // Use word boundary matching to avoid false positives
            topic = await MatchTopicByKeywordsAsync(text);
        }

        topic ??= await _context.ResearchTopics.FirstOrDefaultAsync();

        if (topic != null)
        {
            var topicExists = await _context.PaperTopics.AnyAsync(pt => pt.PaperId == paperId && pt.TopicId == topic.Id);
            if (!topicExists)
            {
                await _context.PaperTopics.AddAsync(new PaperTopic { PaperId = paperId, TopicId = topic.Id });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task<ResearchTopic?> MatchTopicByKeywordsAsync(string text)
    {
        // Machine Learning keywords - use word boundaries where possible
        var mlPatterns = new[] { "machine learning", "deep learning", "neural network", "artificial intelligence",
            "transformer", "bert", "gpt", "lstm", "reinforcement learning", "supervised learning", "unsupervised learning" };
        var mlSingleKeywords = new[] { "cnn", "rnn", "gan", "vae", "mlp" };

        // Computer Vision keywords
        var cvPatterns = new[] { "computer vision", "image recognition", "object detection", "image segmentation" };
        var cvSingleKeywords = new[] { "yolo", "resnet", "faster r-cnn" };

        // NLP keywords
        var nlpPatterns = new[] { "natural language", "sentiment analysis", "machine translation", "language model", "text classification" };
        var nlpSingleKeywords = new[] { "nlp", "llm" };

        // Robotics keywords
        var roboticsPatterns = new[] { "robotics", "autonomous", "motion planning", "robot control" };
        var roboticsSingleKeywords = new[] { "kinematics", "manipulator" };

        // Check patterns (multi-word) first
        if (mlPatterns.Any(p => text.Contains(p)))
            return await GetTopicByNameAsync("Machine Learning");
        if (cvPatterns.Any(p => text.Contains(p)))
            return await GetTopicByNameAsync("Computer Vision");
        if (nlpPatterns.Any(p => text.Contains(p)))
            return await GetTopicByNameAsync("NLP");
        if (roboticsPatterns.Any(p => text.Contains(p)))
            return await GetTopicByNameAsync("Robotics");

        // Check single keywords (with word boundary check to avoid false positives)
        if (mlSingleKeywords.Any(k => HasWordBoundary(text, k)))
            return await GetTopicByNameAsync("Machine Learning");
        if (cvSingleKeywords.Any(k => HasWordBoundary(text, k)))
            return await GetTopicByNameAsync("Computer Vision");
        if (nlpSingleKeywords.Any(k => HasWordBoundary(text, k)))
            return await GetTopicByNameAsync("NLP");
        if (roboticsSingleKeywords.Any(k => HasWordBoundary(text, k)))
            return await GetTopicByNameAsync("Robotics");

        return null;
    }

    private async Task<ResearchTopic?> GetTopicByNameAsync(string topicName)
    {
        return await _context.ResearchTopics
            .FirstOrDefaultAsync(t => EF.Functions.ILike(t.TopicName, topicName));
    }

    private static bool HasWordBoundary(string text, string keyword)
    {
        return text.Contains($" {keyword} ") ||
               text.Contains($" {keyword},") ||
               text.Contains($" {keyword}.") ||
               text.Contains($" {keyword}?") ||
               text.Contains($" {keyword}!") ||
               text.Contains($" {keyword};") ||
               text.Contains($" {keyword}:") ||
               text.Contains($" {keyword}-") ||
               text.Contains($"({keyword} ") ||
               text.StartsWith($"{keyword} ") ||
               text.EndsWith($" {keyword}");
    }
}
