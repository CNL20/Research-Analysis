# Các Chức Năng Hiện Có và Luồng Hoạt Động (Features & Workflows)

Tài liệu này mô tả chi tiết các chức năng đã được triển khai trong hệ thống **ScholarTrend** và các luồng hoạt động (workflow) chính của dự án.

---

## 1. Các Chức Năng Hiện Có (Features)

Hệ thống được chia thành 4 nhóm tính năng (tương đương 4 Phase) đã được hoàn thiện:

### Nhóm 1: Xác thực & Quản lý Người dùng (Auth & User)
- **Đăng ký / Đăng nhập:** Sử dụng JWT Bearer Token để bảo mật. Mật khẩu được mã hóa an toàn.
- **Refresh Token:** Cơ chế cấp lại token tự động khi token cũ hết hạn giúp trải nghiệm người dùng không bị gián đoạn.
- **Quản lý Hồ sơ (Profile):** Cập nhật thông tin cá nhân.
- **Phân quyền (RBAC):** Hệ thống chia làm 3 Roles:
  - `Admin`: Quản trị toàn hệ thống.
  - `Researcher`: Nhà nghiên cứu (được cấp quyền dùng tính năng Compare Trends nâng cao).
  - `LecturerStudent`: Giảng viên / Sinh viên (Role mặc định).

### Nhóm 2: Chức năng Cốt lõi (Core - Search & Bookmark)
- **Tìm kiếm Bài báo (Search Engine):** Tìm kiếm nội bộ có hỗ trợ phân trang, lọc theo Topic, Journal. Tốc độ cao nhờ Index database.
- **Lưu trữ Lịch sử Tìm kiếm:** Tự động ghi lại các từ khóa người dùng đã tìm kiếm để dễ dàng tra cứu lại.
- **Chi tiết Bài báo:** Hiển thị thông tin abstract, tác giả, DOI, ngày xuất bản.
- **Đánh dấu (Bookmark):** Lưu lại các bài báo quan tâm vào bộ sưu tập cá nhân.

### Nhóm 3: Động cơ Xu hướng & Cá nhân hóa (Trend Engine & Personalization)
- **Dashboard Xu hướng:** Thống kê tổng quan biểu đồ tăng trưởng số lượng bài báo, top các từ khóa/chủ đề đang hot.
- **Tính toán Điểm Xu hướng (Trending Score & Growth Rate):** Thuật toán đánh giá sự tăng trưởng của một từ khóa/chủ đề dựa trên số lượng xuất bản theo thời gian.
- **So sánh Xu hướng (Compare Trends):** Chức năng Premium cho phép đặt 2-3 từ khóa lên cùng 1 biểu đồ để so sánh sự quan tâm.
- **Theo dõi (Follow):** Cho phép người dùng bấm "Follow" một Topic (Chủ đề) hoặc Journal (Tạp chí) yêu thích.
- **Thông báo (Notifications):** Hệ thống báo chuông (đánh dấu chưa đọc/đã đọc) khi có cập nhật mới.

### Nhóm 4: Đồng bộ Dữ liệu tự động (Background Jobs)
- **Cào dữ liệu (Sync/Fetch):** Tích hợp Semantic Scholar API thông qua `HttpClient` với chính sách Retry của Polly để lấy bài báo mới.
- **Lập lịch ngầm (Hangfire):** Hệ thống tự động chạy ngầm mỗi ngày (CRON job) để lấy bài báo mới về và cập nhật lại điểm Xu hướng.
- **Admin Dashboard:** Giám sát tiến trình Sync, quản lý danh sách Users.

---

## 2. Luồng Hoạt Động Của Hệ Thống (Workflows)

### Luồng 1: Trải nghiệm Người dùng cơ bản (User Flow)
1. **Truy cập & Đăng nhập:** User mới đăng ký tài khoản -> Nhận Token -> Lưu vào bộ nhớ cục bộ (trên Frontend/Swagger).
2. **Khám phá:** User vào trang Dashboard xem bảng xếp hạng Top Keywords/Topics đang thịnh hành.
3. **Tìm kiếm:** User gõ từ khóa "Machine Learning" vào thanh search -> Hệ thống tự động lưu lịch sử -> Hiển thị danh sách kết quả.
4. **Tương tác:** User click xem chi tiết một bài báo -> Bấm nút **Bookmark** để lưu lại đọc sau.
5. **Cá nhân hóa:** User thích chủ đề "AI", bấm nút **Follow** Topic "AI".

### Luồng 2: Luồng Đồng bộ & Tính toán Xu hướng (Background System Flow)
Đây là "trái tim" của hệ thống diễn ra tự động mà người dùng không nhìn thấy:

1. **Trigger:** Đến 00:00 mỗi ngày, **Hangfire** kích hoạt `SyncJob`.
2. **Fetch Data:** Hệ thống gọi gọi API của **Semantic Scholar** lấy các bài báo xuất bản gần nhất.
3. **Save Database:** Lưu bài báo mới vào SQL Server. Cập nhật các bảng liên quan (Authors, Keywords, Journals).
4. **Tính toán (Recalculate):** Gọi `TrendCalculatorService` tính lại **GrowthRate** và **TrendingScore** cho tháng hiện tại dựa trên số lượng paper mới.
5. **Cập nhật Cache:** Xóa cache cũ, đưa dữ liệu biểu đồ mới vào `IMemoryCache` để user truy cập siêu tốc.
6. **Bắn Thông báo:** Quét xem bài báo mới thuộc Topic/Journal nào. Tìm tất cả User đang **Follow** các Topic/Journal đó và tạo bản ghi **Notification** (chưa đọc) cho họ.

### Luồng 3: Luồng Xử lý Lỗi Gọi API Ngoài (Resilience Flow)
Vì hệ thống phụ thuộc vào `Semantic Scholar`, API bên thứ 3 có thể bị sập hoặc quá tải (Rate limit):
1. Hệ thống gửi Request lấy bài báo.
2. Nếu bị từ chối (HTTP 429 Too Many Requests hoặc 500 Server Error): Thư viện **Polly** sẽ can thiệp.
3. Polly tự động chờ một lát (Exponential Backoff) và gọi lại tối đa 3 lần.
4. Nếu vẫn thất bại: Log lỗi qua **Serilog**, ghi nhận trạng thái Sync là `Failed` trên bảng điều khiển Admin, bỏ qua để mai chạy lại, không làm sập toàn bộ ứng dụng.
