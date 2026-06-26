# Tài liệu Chức năng: Động cơ Xu hướng (Trend Engine) & Cá nhân hóa

ScholarTrend không chỉ cung cấp thông tin bài báo, mà còn phân tích dữ liệu vĩ mô (về Topic, Keyword, Journal) để kết xuất thành Biểu đồ xu hướng, và giúp người dùng cá nhân hóa trải nghiệm.

## 1. Xu hướng & Phân tích (Trends Engine)

Dữ liệu Trend được tính sẵn và Cache lại để giảm tải cho DB.

- `GET /api/Trends/dashboard`: Bảng điều khiển Trend tổng quan, trả về top keywords, topics, journal đang thịnh hành. Được cache tự động.
- Các API trả về biểu đồ Time-series (dữ liệu theo tháng/năm cho Line Chart):
  - `GET /api/Trends/keywords`: Trend theo từ khóa.
  - `GET /api/Trends/topics`: Trend theo chủ đề.
  - `GET /api/Trends/journals`: Trend theo tạp chí.
  - `GET /api/Trends/publications`: Trend tổng quát số lượng bài xuất bản trên toàn hệ thống.
- Các API trả về danh sách TOP (Bảng xếp hạng):
  - `GET /api/Trends/keywords/top`
  - `GET /api/Trends/topics/top`
  - `GET /api/Trends/journals/top`
- `POST /api/Trends/compare`: **Tính năng nâng cao dành riêng cho Researcher (`🔬`)**. So sánh đối chiếu sức ảnh hưởng của nhiều Keyword/Topic trên cùng một hệ trục toạ độ thời gian.

## 2. Theo dõi & Cá nhân hóa (Follows)

Người dùng được quyền Subscribe theo dõi thông tin mới nhất.

- Các API lấy danh sách đang Follow:
  - `GET /api/Follows/topics`
  - `GET /api/Follows/journals`
  - `GET /api/Follows/authors`
  - `GET /api/Follows/papers`
- Các API Thêm/Hủy Theo dõi. Nhận vào tham số `{id}` trên đường dẫn:
  - `POST` / `DELETE /api/Follows/topics/{topicId}`
  - `POST` / `DELETE /api/Follows/journals/{journalId}`
  - `POST` / `DELETE /api/Follows/authors/{authorId}`
  - `POST` / `DELETE /api/Follows/papers/{paperId}`

## 3. Dashboard Cá nhân & Thông báo

Hệ thống kết hợp dữ liệu Bookmark, Lịch sử, Follow để tạo không gian riêng biệt.

- `GET /api/Dashboard/personal`: Tổng hợp dữ liệu cá nhân hóa (số liệu bookmark, top các chủ đề bạn quan tâm, đề xuất bài viết liên quan).
- `GET /api/Dashboard/overview`: Dành cho trang chủ Public tổng quan nhất.
- `GET /api/Notifications`: Đọc thông báo cá nhân (khi có bài viết mới thuộc Topic đang theo dõi, v.v.).
- `GET /api/Notifications/unread-count`: Cập nhật số lượng huy hiệu chấm đỏ chưa đọc.
- `HttpPatch /api/Notifications/{id}/read` & `read-all`: Đánh dấu thông báo đã xem.
- `GET` / `PUT /api/Notifications/settings`: Điều chỉnh cấu hình cài đặt nhận thông báo.
