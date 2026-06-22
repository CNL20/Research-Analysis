# ScholarTrend

**Hệ thống theo dõi xu hướng công bố báo chí khoa học** (Scientific Journal Publication Trend Tracking System).

Dự án SWP — nhóm SU26SWP06. Backend REST API xây dựng trên **.NET 9**, kiến trúc **Clean Architecture**, cung cấp đầy đủ chức năng từ xác thực người dùng, tìm kiếm bài báo, phân tích xu hướng, cá nhân hóa, đồng bộ dữ liệu ngoài, đến dashboard và báo cáo.

---

## 📚 Tài liệu Dự án (Documentation)

Để dễ dàng theo dõi và bảo trì, toàn bộ tài liệu hướng dẫn và đặc tả API của hệ thống đã được phân tách thành các chuyên đề chi tiết trong thư mục `docs/`. Vui lòng nhấn vào các liên kết bên dưới để xem chi tiết:

1. 🏛️ **[Tổng quan Kiến trúc & Công nghệ (Tech Stack & Architecture)](docs/01-architecture-tech.md)**
   - Phân tầng Clean Architecture.
   - Các công nghệ áp dụng (.NET 9, Hangfire, JWT...).
   - Role người dùng trong hệ thống.

2. 🚀 **[Hướng dẫn Cài đặt & Chạy Dự án (Setup Guide)](docs/02-setup-guide.md)**
   - Cấu hình chuỗi kết nối Database và appsettings.
   - Cơ chế tạo Dữ liệu mẫu (Seeder).
   - Tài khoản Test mặc định.
   - Hướng dẫn dùng Swagger.

3. 🔐 **[Chức năng Xác thực & Người dùng (Auth Features)](docs/03-auth-features.md)**
   - Cơ chế hoạt động của JWT Access Token & Refresh Token.
   - Các API Đăng ký, Đăng nhập (Google Login).
   - Xác thực Email qua Brevo, Quên/Đổi mật khẩu.
   - Profile người dùng.

4. 🔍 **[Chức năng Tìm kiếm & Cốt lõi (Core Features)](docs/04-core-features.md)**
   - API Tìm kiếm, Danh sách Bài báo, Chủ đề (Topics), Tạp chí (Journals), Tác giả.
   - Tính năng lưu trữ Bookmark bài báo.

5. 📈 **[Động cơ Xu hướng & Cá nhân hóa (Trend & Personalization)](docs/05-trend-personalization.md)**
   - API xuất biểu đồ Xu hướng (Line Charts, Dashboards).
   - Chức năng theo dõi (Follows).
   - Hệ thống Thông báo (Notifications).

6. ⚙️ **[Quản trị viên, Xuất Báo cáo & Đồng bộ (Admin & Sync)](docs/06-admin-sync.md)**
   - Quản trị viên (Phân quyền, bật/tắt User).
   - Kích hoạt Đồng bộ dữ liệu (Data Sync) qua Hangfire từ Semantic Scholar, OpenAlex.
   - Xuất (Export) báo cáo dữ liệu định dạng JSON, CSV.

---

*ScholarTrend — SU26SWP06 · .NET 9 · Clean Architecture*
