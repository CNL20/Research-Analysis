# Tài liệu Chức năng: Tìm kiếm & Quản lý Thông tin Cốt lõi

Các tính năng lõi của ScholarTrend xoay quanh việc tìm kiếm, xem chi tiết bài báo, tác giả, tạp chí, chủ đề và lưu trữ (bookmark).

Tất cả các API dưới đây đều yêu cầu xác thực `🔒 Auth` qua JWT.

## 1. Tìm kiếm và Bài báo (Papers)

- `GET /api/Papers/aggregate`: Liệt kê và tính toán tổng quát (aggregate) các bài báo trên hệ thống.
- `GET /api/Papers/search`: API tìm kiếm cốt lõi đa tiêu chí. Hỗ trợ tìm theo `keyword`, `author`, `journal`, hoặc `all`. Các bộ lọc như khoảng thời gian (`yearFrom`, `yearTo`), số lượng trích dẫn, phân trang `page` / `pageSize` đều được tích hợp. Mỗi lần gọi API, hệ thống sẽ **tự động lưu lại Lịch sử tìm kiếm** cho User.
- `GET /api/Papers/{id}`: Xem chi tiết 1 bài báo (Tiêu đề, Tóm tắt, Danh sách Tác giả, Topic, Keyword). Trả về biến boolean báo hiệu người dùng có Bookmark bài này chưa.
- `GET /api/Papers/{id}/aggregate`: Tính toán thêm dữ liệu bổ sung cho 1 bài báo.
- `GET /api/Papers/by-topic/{topicId}`: Lọc bài báo thuộc một chủ đề cụ thể.
- `GET /api/Papers/by-journal/{journalId}`: Lọc bài báo thuộc tạp chí cụ thể.
- `GET /api/Papers/search-history`: Xem lại các từ khóa và bộ lọc tìm kiếm gần nhất mà người dùng này từng dùng.

## 2. Chủ đề (Topics) & Tạp chí (Journals) & Tác giả (Authors)

- `GET /api/Topics`: Danh sách các chủ đề (Kèm theo Paper Count).
- `GET /api/Topics/{id}`: Chi tiết một chủ đề bao gồm thông tin phân tích xu hướng (Trend Chart).
- `GET /api/Journals`: Danh sách các tạp chí uy tín.
- `GET /api/Journals/{id}`: Chi tiết một tạp chí bao gồm Impact Factor và biểu đồ tăng trưởng (Trend).
- `GET /api/Authors`: Danh sách các tác giả nổi bật trên thế giới.
- `GET /api/Authors/{id}`: Thông tin hồ sơ, chỉ số (h-index) và danh sách bài báo của tác giả.
- `GET /api/Authors/by-name`: Tra cứu tác giả thông qua chuỗi tìm kiếm bằng tên.

## 3. Quản lý Đánh dấu (Bookmarks)

Cho phép người dùng "Lưu lại đọc sau".

- `GET /api/Bookmarks`: Xem danh sách tất cả các bài báo đang được đánh dấu bởi người dùng.
- `POST /api/Bookmarks/{paperId}`: Thêm bài báo vào danh sách lưu trữ.
- `DELETE /api/Bookmarks/{paperId}`: Gỡ bài báo khỏi danh sách.
