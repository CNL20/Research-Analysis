using Microsoft.EntityFrameworkCore;
using ScholarTrend.Domain.Entities;
using ScholarTrend.Infrastructure.Data;

namespace ScholarTrend.Infrastructure.Data.Seeders;

public static class ResearchPaperSeeder
{
    private sealed record PaperSeedSpec(
        string Title,
        string Abstract,
        DateTime PublicationDate,
        int CitationCount,
        string Doi,
        int JournalIndex,
        int[] AuthorIndexes,
        int[] KeywordIndexes,
        int[] TopicIndexes);

    public static async Task<List<ResearchPaper>> SeedAsync(
        ScholarTrendDbContext context,
        List<Journal> journals,
        List<Author> authors,
        List<Keyword> keywords,
        List<ResearchTopic> topics)
    {
        if (await context.ResearchPapers.AnyAsync())
        {
            return await context.ResearchPapers.OrderBy(p => p.Id).ToListAsync();
        }

        var specs = new List<PaperSeedSpec>
        {
            new(
                "Explainable Transformer Models for Medical Image Segmentation",
                "This study explores transformer architectures for segmentation tasks and combines attention maps with explainability techniques to support clinical interpretation.",
                new DateTime(2025, 6, 14),
                128,
                "10.1000/st.2025.0001",
                0,
                [0, 3],
                [0, 2, 4],
                [0]),
            new(
                "Federated Learning for Privacy-Preserving Cyber Threat Detection",
                "The paper proposes a federated learning pipeline for distributed intrusion detection while preserving data privacy across organizations.",
                new DateTime(2025, 6, 28),
                96,
                "10.1000/st.2025.0002",
                2,
                [1, 6, 9],
                [1, 7, 8],
                [3]),
            new(
                "Graph Neural Networks for Large-Scale Recommendation Systems",
                "A scalable graph neural network framework is introduced for user-item recommendation under sparse interaction signals.",
                new DateTime(2025, 7, 10),
                141,
                "10.1000/st.2025.0003",
                4,
                [2, 4],
                [1, 3, 8],
                [1]),
            new(
                "Blockchain-Enabled Data Integrity in Cloud Native Applications",
                "This work investigates blockchain-backed auditing mechanisms to guarantee integrity in cloud-native data pipelines and microservices.",
                new DateTime(2025, 7, 24),
                82,
                "10.1000/st.2025.0004",
                2,
                [3, 7],
                [6, 8, 9],
                [4]),
            new(
                "A Survey of Natural Language Processing for Low-Resource Languages",
                "The survey reviews transfer learning, language modeling, and evaluation strategies for NLP tasks in low-resource settings.",
                new DateTime(2025, 8, 5),
                154,
                "10.1000/st.2025.0005",
                3,
                [0, 8],
                [0, 5, 1],
                [0, 1]),
            new(
                "Energy-Efficient Internet of Things Architecture for Smart Cities",
                "We design an energy-aware IoT architecture for smart-city sensing, communication, and operational analytics.",
                new DateTime(2025, 8, 18),
                73,
                "10.1000/st.2025.0006",
                2,
                [1, 5],
                [9, 8, 7],
                [4]),
            new(
                "Deep Learning for Real-Time Object Detection in Autonomous Systems",
                "This paper benchmarks lightweight deep learning detectors for real-time perception in autonomous vehicles and robotics.",
                new DateTime(2025, 9, 2),
                167,
                "10.1000/st.2025.0007",
                0,
                [2, 3, 9],
                [2, 4, 1],
                [0, 2]),
            new(
                "Mining Software Repositories for Defect Prediction at Scale",
                "A data mining pipeline is presented for defect prediction using code metrics, commit histories, and repository mining features.",
                new DateTime(2025, 9, 16),
                109,
                "10.1000/st.2025.0008",
                3,
                [1, 7],
                [3, 8, 1],
                [2]),
            new(
                "Adaptive Big Data Pipelines for Scientific Analytics",
                "The study designs adaptive data processing pipelines that optimize throughput and cost for scientific analytics workloads.",
                new DateTime(2025, 10, 4),
                91,
                "10.1000/st.2025.0009",
                1,
                [0, 4, 6],
                [8, 3, 1],
                [1]),
            new(
                "Hybrid Cloud Scheduling Using Reinforcement Learning",
                "This paper introduces reinforcement learning-based scheduling policies for hybrid cloud resource allocation and load balancing.",
                new DateTime(2025, 10, 22),
                118,
                "10.1000/st.2025.0010",
                4,
                [5, 8],
                [8, 1, 9],
                [4]),
            new(
                "Secure Multi-Party Computation for Collaborative Healthcare Data Sharing",
                "We propose privacy-preserving protocols that enable collaborative analytics over sensitive healthcare datasets.",
                new DateTime(2025, 11, 7),
                87,
                "10.1000/st.2025.0011",
                0,
                [3, 6],
                [7, 0, 8],
                [3]),
            new(
                "Attention-Based Sentiment Analysis in Social Media Streams",
                "The paper evaluates attention-based architectures for sentiment analysis under noisy and rapidly changing social media content.",
                new DateTime(2025, 11, 21),
                104,
                "10.1000/st.2025.0012",
                2,
                [2, 9],
                [5, 1, 2],
                [0, 1]),
            new(
                "Self-Supervised Learning for Remote Sensing Image Classification",
                "A self-supervised framework is developed for remote sensing classification when annotated samples are limited.",
                new DateTime(2025, 12, 3),
                132,
                "10.1000/st.2025.0013",
                4,
                [0, 4],
                [2, 4, 8],
                [0]),
            new(
                "Anomaly Detection in IoT Networks with Autoencoders",
                "The study applies autoencoders to detect anomalies in IoT traffic and edge telemetry in near real time.",
                new DateTime(2025, 12, 19),
                95,
                "10.1000/st.2025.0014",
                2,
                [1, 6, 8],
                [9, 7, 2],
                [3, 4]),
            new(
                "Cross-Domain Knowledge Transfer for Smart Manufacturing",
                "This research explores knowledge transfer methods for predictive maintenance and smart manufacturing scenarios.",
                new DateTime(2026, 1, 8),
                76,
                "10.1000/st.2026.0015",
                1,
                [3, 5],
                [0, 1, 8],
                [2, 4]),
            new(
                "LLM-Assisted Code Review for Software Engineering Productivity",
                "We evaluate large language model assistants for code review workflows and productivity improvements in software teams.",
                new DateTime(2026, 1, 25),
                149,
                "10.1000/st.2026.0016",
                3,
                [2, 7, 9],
                [0, 1, 5],
                [2]),
            new(
                "Scalable Vector Search for Enterprise Retrieval-Augmented Systems",
                "The paper introduces a scalable vector search pipeline that improves retrieval performance for enterprise RAG systems.",
                new DateTime(2026, 2, 6),
                111,
                "10.1000/st.2026.0017",
                4,
                [4, 8],
                [1, 8, 0],
                [0, 2]),
            new(
                "Privacy-Aware Data Mining in Financial Fraud Detection",
                "A privacy-aware data mining approach is presented for robust financial fraud detection across distributed datasets.",
                new DateTime(2026, 2, 20),
                88,
                "10.1000/st.2026.0018",
                2,
                [1, 5],
                [3, 7, 8],
                [1, 3]),
            new(
                "Computer Vision Based Crop Monitoring with Edge AI",
                "The paper combines computer vision and edge AI to monitor crops, identify stress patterns, and support precision agriculture.",
                new DateTime(2026, 3, 12),
                123,
                "10.1000/st.2026.0019",
                0,
                [0, 6, 9],
                [4, 2, 9],
                [0, 4]),
            new(
                "Benchmarking Cloud Resource Optimization Strategies for AI Workloads",
                "This benchmark compares optimization strategies for cloud resource allocation in GPU-heavy AI training workloads.",
                new DateTime(2026, 5, 15),
                160,
                "10.1000/st.2026.0020",
                1,
                [3, 4, 8],
                [1, 8, 0],
                [4])
        };

        var papers = new List<ResearchPaper>();
        var paperAuthors = new List<PaperAuthor>();
        var paperKeywords = new List<PaperKeyword>();
        var paperTopics = new List<PaperTopic>();

        for (var index = 0; index < specs.Count; index++)
        {
            var spec = specs[index];
            var paper = new ResearchPaper
            {
                Title = spec.Title,
                Abstract = spec.Abstract,
                PublicationDate = spec.PublicationDate,
                PublicationYear = spec.PublicationDate.Year,
                Doi = spec.Doi,
                Url = $"https://doi.org/{spec.Doi}",
                PdfUrl = null,
                CitationCount = spec.CitationCount,
                Status = Domain.Enums.PaperStatus.Available,
                CreatedAt = spec.PublicationDate.AddDays(1),
                UpdatedAt = null,
                JournalId = journals[spec.JournalIndex].Id,
                PaperSources = new List<PaperSource>
                {
                    new()
                    {
                        SourceName = "ScholarTrend Seed",
                        ExternalId = $"PAPER-{index + 1:0000}",
                        SourceDoi = spec.Doi,
                        SourceUrl = $"https://doi.org/{spec.Doi}",
                        SourceCitationCount = spec.CitationCount,
                        FetchedAt = spec.PublicationDate.AddDays(1),
                        LastSeenAt = spec.PublicationDate.AddDays(1)
                    }
                }
            };

            papers.Add(paper);
        }

        await context.ResearchPapers.AddRangeAsync(papers);
        await context.SaveChangesAsync();

        for (var index = 0; index < specs.Count; index++)
        {
            var spec = specs[index];
            var paper = papers[index];

            foreach (var authorIndex in spec.AuthorIndexes)
            {
                paperAuthors.Add(new PaperAuthor
                {
                    PaperId = paper.Id,
                    AuthorId = authors[authorIndex].Id,
                    AuthorOrder = paperAuthors.Count(pa => pa.PaperId == paper.Id) + 1
                });
            }

            foreach (var keywordIndex in spec.KeywordIndexes)
            {
                paperKeywords.Add(new PaperKeyword
                {
                    PaperId = paper.Id,
                    KeywordId = keywords[keywordIndex].Id
                });
            }

            foreach (var topicIndex in spec.TopicIndexes)
            {
                paperTopics.Add(new PaperTopic
                {
                    PaperId = paper.Id,
                    TopicId = topics[topicIndex].Id
                });
            }
        }

        await context.PaperAuthors.AddRangeAsync(paperAuthors);
        await context.PaperKeywords.AddRangeAsync(paperKeywords);
        await context.PaperTopics.AddRangeAsync(paperTopics);
        await context.SaveChangesAsync();

        return papers;
    }
}
