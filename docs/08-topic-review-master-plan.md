# Tài liệu Thiết kế: AI Topic Insights Dashboard (Groq Integration)

Tài liệu này mô tả kiến trúc và luồng xử lý của tính năng **AI Topic Insights**, tính năng cốt lõi giúp phân tích xu hướng nghiên cứu và trực quan hóa dữ liệu trên Dashboard của ScholarTrend, hoàn toàn đáp ứng yêu cầu của đồ án SWP391.

---

## 1. Mục tiêu (North Star)

**Đầu vào (Input):**
- Danh sách các bài báo khoa học (Research Papers) thuộc về một Chủ đề (Topic) nhất định đã được thu thập thông qua quá trình Sync (OpenAlex/Semantic Scholar).

**Đầu ra (Output Dashboard):**
Hệ thống tự động xử lý và xuất ra một Dashboard trực quan gồm 4 phần chính:
1. **Top methods**: Các phương pháp nghiên cứu được sử dụng nhiều nhất.
2. **Top datasets**: Các tập dữ liệu (dataset) phổ biến nhất trong topic.
3. **Timeline**: Dòng thời gian phát triển và các thành tựu nổi bật theo năm.
4. **Opportunities (Cơ hội nghiên cứu)**: AI tự động phân tích và đề xuất các cơ hội nghiên cứu tiềm năng dựa trên phần "Future Work" của các bài báo.

---

## 2. Kiến trúc tổng thể

Việc xử lý 1 khối lượng lớn bài báo không thể thực hiện theo thời gian thực (real-time) mỗi khi user truy cập. Giải pháp tối ưu:
- **Background Job**: Sử dụng `TopicInsightAggregationJob` chạy ngầm.
- **Tích hợp Groq AI**: Chuyên dùng cho tác vụ Tóm tắt (Summarization) vì tốc độ nhanh và hạn mức (quota) dư dả.
- **Cache Data**: Kết quả lưu sẵn vào Database (`TopicInsight`) để API gọi lấy dữ liệu tức thì.

---

## 3. Database Schema

Hệ thống sử dụng các bảng sau để lưu trữ kết quả phân tích:

- **`TopicInsight`**: Lưu kết quả tổng hợp cấp Topic (theo từng năm).
  - Cột chứa JSON: `TopMethodsJson`, `TopDatasetsJson`, `FutureDirectionsJson`.
  - Cột Text: `Achievement`, `Summary`.
- **`TopicInsightEvidence`**: Bảng quan trọng giúp "Trace" (truy xuất nguồn gốc) – mỗi một Opportunity do AI đưa ra đều được link ngược lại với ID của bài báo gốc để đảm bảo tính minh bạch.
- **`PaperTopicExtraction`**: Lưu thông tin đã trích xuất của TỪNG bài báo. Tránh việc gọi AI trùng lặp nhiều lần cho cùng một bài báo.

---

## 4. Pipeline chi tiết (Luồng xử lý)

Quá trình tổng hợp được thực hiện trong `TopicInsightAggregationJob.cs` theo các bước:

### Bước 1: Lọc dữ liệu
Hệ thống tìm kiếm các Topics có dữ liệu bài báo mới nhưng chưa có bản ghi `TopicInsight` của năm hiện tại.

### Bước 2: Gom nhóm Deterministic (Không dùng AI)
- Đọc `MethodsJson` và `DatasetsJson` từ hàng loạt bài báo thuộc Topic.
- Tính toán tần suất xuất hiện.
- Chọn ra **Top 5 Methods** và **Top 5 Datasets** phổ biến nhất (để vẽ Cloud Tags/Biểu đồ).

### Bước 3: AI Summarization (Dùng Groq)
- Lọc ra tối đa 15 đoạn `FutureWorkJson` tiêu biểu nhất từ các bài báo.
- Gọi **Groq API** (`_aiExtractionService.SummarizeOpportunitiesAsync`) với prompt yêu cầu AI tóm tắt 15 đoạn này thành các "Cơ hội nghiên cứu (Opportunities)" ngắn gọn.
- (Fallback): Nếu Topic chưa có Methods/Datasets, Groq có thể đóng vai trò dự đoán bù trừ.

### Bước 4: Lưu trữ & Tạo Evidence
- Sinh bản ghi `TopicInsight` mới.
- Ứng với mỗi Opportunity, lưu các bản ghi `TopicInsightEvidence` để Frontend có thể làm tính năng "Bấm vào để xem nguồn gốc (View Source Paper)".

---

## 5. Quyết định kỹ thuật & Lý do

| Quyết định | Tại sao |
|---|---|
| **Chỉ xử lý Metadata (Abstract, Keywords)** | Việc parse Full-text (PDF) tốn chi phí lớn, dễ vi phạm bản quyền và nằm ngoài phạm vi môn học SWP391. |
| **Gom nhóm Method/Dataset bằng Code (thay vì AI)** | Tránh tình trạng AI "ảo giác" (hallucinate) tự chế ra các số liệu phần trăm không có thật. Thống kê bằng code đảm bảo độ chính xác tuyệt đối. |
| **Sử dụng Groq AI thay vì OpenAI** | Groq cho tốc độ inference (suy luận) cực nhanh và miễn phí/rẻ hơn rất nhiều, hoàn toàn đủ thông minh để làm tác vụ tóm tắt văn bản. |
| **Giữ lại Evidence Graph** | Tạo độ tin cậy (Academic credibility) – Báo cáo không phải là bịa đặt, người dùng có thể đối chiếu. |

---
*ScholarTrend — SU26SWP06 · AI Topic Insights Dashboard*
