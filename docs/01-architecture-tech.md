# Tổng quan Kiến trúc & Công nghệ

Dự án ScholarTrend tuân thủ chặt chẽ nguyên lý **Clean Architecture**, giúp hệ thống dễ bảo trì, dễ mở rộng và tách biệt hoàn toàn giữa Core Logic với các thành phần bên ngoài (như Database hay Web Framework).

## 1. Kiến trúc 4 Tầng (Clean Architecture)

Hệ thống được chia làm 4 layer chính, giao tiếp theo nguyên tắc Dependency Inversion (các tầng ngoài phải phụ thuộc vào tầng trong):

1. **ScholarTrend.Domain (Tầng Cốt lõi)**
   - Chứa các Entities, Enums, Constants.
   - Hoàn toàn **không phụ thuộc** vào bất kỳ layer hay thư viện bên ngoài nào.

2. **ScholarTrend.Application (Tầng Nghiệp vụ - Use Cases)**
   - Chứa các `Interfaces` cho Repositories và Services.
   - Chứa các `DTOs` (Data Transfer Objects) và `Validators` (FluentValidation).
   - Chứa toàn bộ **Business Logic** trong các class `Service` (ví dụ: `AuthService`, `SyncService`).
   - Phụ thuộc vào `ScholarTrend.Domain`.

3. **ScholarTrend.Infrastructure (Tầng Hạ tầng)**
   - Triển khai các Interface của tầng Application.
   - Chứa `DbContext` (EF Core), cấu hình Database, Migrations, Seeders.
   - Các `Repository` tương tác trực tiếp với SQL Server.
   - Các dịch vụ bên ngoài (External APIs, Email Service bằng Brevo, Hangfire Jobs).
   - Phụ thuộc vào `ScholarTrend.Application`.

4. **ScholarTrend.API (Tầng Giao tiếp)**
   - Đóng vai trò là Web API (Controllers).
   - Middleware (bắt lỗi, JWT Authentication).
   - Cấu hình DI (Dependency Injection), Swagger, Hangfire Dashboard.
   - Các `Controller` nhận HTTP Request, gọi `Service` từ Application layer và trả về JSON.
   - Phụ thuộc vào `ScholarTrend.Application` và `ScholarTrend.Infrastructure`.

## 2. Công nghệ sử dụng

| Thành phần | Công nghệ / Thư viện |
|---|---|
| **Runtime & Framework** | .NET 9, ASP.NET Core Web API |
| **Database & ORM** | SQL Server, Entity Framework Core 9 |
| **Authentication** | ASP.NET Identity, JWT Bearer Token |
| **Validation** | FluentValidation |
| **Background Jobs** | Hangfire (lưu state vào SQL Server) |
| **Caching** | IMemoryCache (cho Dashboard trend, TTL 1 giờ) |
| **Email Service** | Brevo REST API (thay thế SMTP) |
| **API Documentation** | Swagger / OpenAPI |

## 3. Vai trò Người dùng (Roles)

Hệ thống phân quyền theo 3 cấp độ:

- **Admin (`👑`)**: Toàn quyền. Quản lý người dùng, xem lịch sử và kích hoạt đồng bộ dữ liệu bằng tay. Xem Admin Dashboard.
- **Researcher (`🔬`)**: Quyền nâng cao. Có thể xuất (export) báo cáo JSON/CSV và sử dụng chức năng so sánh nhiều Trends trên cùng một biểu đồ.
- **LecturerStudent (`🔒`)**: Quyền cơ bản. Chỉ được tìm kiếm, xem bài báo, bookmark, follow topic/journal, và nhận thông báo cá nhân. (Là Role mặc định khi người dùng mới đăng ký).
