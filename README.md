# ScholarTrend Project Progress

Hệ thống theo dõi xu hướng công bố báo chí khoa học (Scientific Journal Publication Trend Tracking System).

## 🚀 Tiến độ dự án

### Phase 1: Backend Foundation & Authentication
- [x] **Application Layer Setup**
  - [x] Tạo DTOs cho Authentication (Register, Login, Profile)
  - [x] Tạo Repository interfaces (`IGenericRepository`, `IUnitOfWork`)
  - [x] Tạo `IAuthService` interface
  - [x] Cấu hình `ApiResponse` chuẩn hóa kết cục trả về
- [x] **Infrastructure Layer Implementation**
  - [x] Triển khai `GenericRepository<T>` dùng EF Core
  - [x] Triển khai `UnitOfWork`
- [x] **Authentication Logic**
  - [x] Triển khai `AuthService`: Register (role mặc định), Login, GetProfile
  - [x] Logic tạo JWT Token với đầy đủ Claims
- [x] **API Layer Setup**
  - [x] Tạo `AuthController` với 3 endpoints chính
  - [x] Cấu hình Dependency Injection trong `Program.cs`
  - [x] Cấu hình Swagger hỗ trợ nhập JWT Bearer Token
  - [x] Cấu hình Hangfire Background Job
- [x] **Verification**
  - [x] Build dự án thành công (Fix lỗi NU1605 downgrade package)
  - [x] Test endpoints qua Swagger (Đã sẵn sàng để chạy)

### Phase 2: Core Features (In Progress 🏗️)
- [x] **Repository Layer Implementation** (Ưu tiên)
  - [x] Tạo interface repository riêng cho `ResearchPaper`, `Bookmark`, `ResearchTopic`, `Journal`
  - [x] Triển khai Repository classes với các query chuyên biệt (Search, Filter, Include)
  - [x] Cập nhật `IUnitOfWork` và `UnitOfWork` (Lazy loading repositories)
  - [x] Đăng ký Dependency Injection trong `Program.cs` cho các repository mới
- [ ] **Service & DTO Implementation** (Tiếp theo)
  - [ ] Tạo DTOs cho Paper, Bookmark, Topic, Journal
  - [ ] Triển khai các Service xử lý Logic

---

## 🛠️ Hướng dẫn chạy dự án

### Yêu cầu
- .NET 9 SDK
- SQL Server (LocalDB hoặc Server thật)

### Chạy Backend
1. Mở terminal tại thư mục gốc.
2. Chạy lệnh: `dotnet run --project ScholarTrend.API`
3. Truy cập Swagger UI: `https://localhost:<port>/swagger`
4. Truy cập Hangfire Dashboard: `https://localhost:<port>/hangfire`

---

## 📝 Ghi chú phase 1
- **Auth:** Sử dụng ASP.NET Identity kết hợp JWT.
- **Role:** Người dùng đăng ký mới mặc định có role `LecturerStudent`.
- **Database:** Tự động Migration và Seed dữ liệu mẫu khi khởi động app.
