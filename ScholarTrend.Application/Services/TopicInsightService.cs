using ScholarTrend.Application.DTOs.TopicInsights;
using ScholarTrend.Application.Interfaces;

namespace ScholarTrend.Application.Services;

public class TopicInsightService : ITopicInsightService
{
    public Task<TopicInsightDashboardDto> GetTopicInsightDashboardAsync(int topicId)
    {
        // MOCK DATA for Phase 1.5
        var mockData = new TopicInsightDashboardDto
        {
            TopicId = topicId,
            TopicName = "Artificial Intelligence",
            LastAnalyzedAt = DateTime.UtcNow,
            TopMethods = new List<string> { "Convolutional Neural Networks", "Transformers", "Random Forest" },
            TopDatasets = new List<string> { "ImageNet", "Kaggle COVID-19", "MIMIC-CXR" },
            Timeline = new List<TimelineDto>
            {
                new TimelineDto
                {
                    Year = 2021,
                    Achievement = "Vision Transformers bắt đầu vượt qua CNN trong phân tích ảnh y tế.",
                    Summary = "Sự trỗi dậy của Transformer trong Computer Vision.",
                    PaperCount = 45
                },
                new TimelineDto
                {
                    Year = 2022,
                    Achievement = "Các mô hình Foundation được áp dụng rộng rãi vào chẩn đoán lâm sàng.",
                    Summary = "Tích hợp AI vào Workflow thực tế.",
                    PaperCount = 120
                },
                new TimelineDto
                {
                    Year = 2023,
                    Achievement = "Multimodal AI (kết hợp hình ảnh và văn bản) đạt độ chính xác đột phá.",
                    Summary = "AI đa phương thức lên ngôi.",
                    PaperCount = 230
                }
            },
            Opportunities = new List<ResearchOpportunityDto>
            {
                new ResearchOpportunityDto
                {
                    Title = "Phân tích đa phương thức (Multimodal diagnosis) còn chưa được khai thác sâu",
                    Description = "Hầu hết các nghiên cứu chỉ tập trung vào hình ảnh hoặc văn bản riêng lẻ. Việc kết hợp dữ liệu X-quang và hồ sơ bệnh án điện tử (EHR) đồng thời vẫn là một khoảng trống lớn cần giải quyết.",
                    Evidences = new List<EvidenceDto>
                    {
                        new EvidenceDto { PaperId = 101, Excerpt = "Current methods lack the ability to effectively fuse visual features from X-rays with sequential text data from EHRs." },
                        new EvidenceDto { PaperId = 105, Excerpt = "Future work should focus on unified multimodal architectures to reduce false positive rates." }
                    }
                },
                new ResearchOpportunityDto
                {
                    Title = "Vấn đề thiên lệch dữ liệu (Data Bias) ở các nhóm thiểu số",
                    Description = "Các mô hình hiện tại được huấn luyện chủ yếu trên dữ liệu của người da trắng. Hiệu suất của AI giảm rõ rệt khi áp dụng cho các nhóm dân tộc thiểu số.",
                    Evidences = new List<EvidenceDto>
                    {
                        new EvidenceDto { PaperId = 203, Excerpt = "We observed a 15% drop in accuracy when evaluating the model on diverse demographic cohorts not represented in the training set." }
                    }
                }
            }
        };

        return Task.FromResult(mockData);
    }
}
