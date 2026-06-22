# Tài liệu Chức năng: Admin, Xuất Báo cáo & Đồng bộ Dữ liệu

Phần này bao gồm những công cụ điều phối mạnh mẽ phục vụ quản trị hệ thống và người làm công tác nghiên cứu chuyên sâu.

## 1. Hệ thống Quản trị Viên (Admin API)

Tất cả các API này yêu cầu phân quyền mức `👑 Admin`.

- `GET /api/AdminUsers`: Lấy danh sách toàn bộ người dùng (phân trang, lọc theo tên, email, vai trò).
- `GET /api/AdminUsers/{id}`: Xem chi tiết profile của một user cụ thể.
- Đổi trạng thái hoặc vai trò:
  - Bật/Tắt tài khoản (Status).
  - Nâng cấp Quyền (Từ `LecturerStudent` sang `Researcher` hoặc `Admin`).
- `GET /api/AdminDashboard`: Lấy thông số báo cáo toàn hệ thống phục vụ cho trang Chart Dashboard của riêng Admin (Total Users, Users by role, Total Papers, Lịch sử Sync...).

## 2. Data Sync & Hangfire Background Jobs

Chức năng Đồng bộ hóa tự động gọi API ra các hệ thống thế giới (như Semantic Scholar, OpenAlex) để kéo bài báo khoa học mới về. Quá trình có thể chạy tự động hàng ngày bằng **Hangfire** hoặc kích hoạt bằng tay.

- `POST /api/AdminSync/trigger`: Admin bấm nút trên web để cưỡng bức việc gọi API Sync và update dữ liệu ngay lập tức.
- `GET /api/AdminSync/logs`: Truy vấn lịch sử các lần Sync trong quá khứ (Thời gian chạy, Số bản ghi kéo về được, Báo lỗi).
- `GET /api/AdminSync/data-sources`: Liệt kê các thư viện Data Sources đang được hỗ trợ.

## 3. Chức năng Xuất Báo cáo (Reports)

Đây là chức năng dành cho **Researcher (`🔬`)** để làm căn cứ nghiên cứu và trích xuất File.

- `GET /api/Reports/publications`: Báo cáo dưới dạng Object JSON hiển thị trên bảng lưới.
- `GET /api/Reports/export/json`: Trực tiếp xuất dữ liệu Trend hoặc Publications ra định dạng Tệp `.json` (download).
- `GET /api/Reports/export/csv`: Xuất dữ liệu ra định dạng Excel / `.csv` để Researcher nhập vào công cụ phân tích SPSS / Excel.
