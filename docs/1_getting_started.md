# 1. Getting Started (Cài đặt & Hướng dẫn)

Tài liệu này hướng dẫn cách cấu hình môi trường, chạy dự án và danh sách tài khoản test có sẵn.

## Yêu cầu hệ thống

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB, Express, hoặc full instance)
- (Tuỳ chọn) Postman hoặc curl để test API

## Bước 1 — Clone & cấu hình

```bash
git clone <repo-url>
cd Research-Analysis
```

Tạo file `ScholarTrend.API/appsettings.Development.json` (không commit file này nếu chứa secret):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=ScholarTrendDb;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Authentication": {
    "Jwt": {
      "SecretKey": "YourSuperSecretKeyAtLeast32CharactersLong!",
      "ExpirationMinutes": 60,
      "RefreshTokenExpirationDays": 7
    }
  }
}
```

Hoặc set biến môi trường:

```bash
set JWT_SECRET_KEY=YourSuperSecretKeyAtLeast32CharactersLong!
set ASPNETCORE_ENVIRONMENT=Development
```

> **Lưu ý:** `SecretKey` trong `appsettings.json` để trống — bắt buộc cấu hình qua Development file hoặc env var. App sẽ throw exception nếu thiếu.

### External APIs (Sync)

Trong `appsettings.json`:
```json
"ExternalApis": {
  "SemanticScholar": {
    "BaseUrl": "https://api.semanticscholar.org/graph/v1",
    "SearchQuery": "artificial intelligence",
    "PageSize": 10
  },
  "OpenAlex": {
    "BaseUrl": "https://api.openalex.org",
    "SearchQuery": "machine learning",
    "PageSize": 10
  }
}
```

## Bước 2 — Chạy dự án

```bash
dotnet run --project ScholarTrend.API --launch-profile http
```

Hoặc để build toàn bộ system và chạy Unit Test:

```bash
# Build toàn solution
dotnet build ScholarTrend.sln

# Chạy tests
dotnet test ScholarTrend.Tests
```

## Bước 3 — Truy cập Dashboard

| Dịch vụ | URL | Mô tả |
|---|---|---|
| **Swagger UI** | `http://localhost:5141/swagger` | Document và test API |
| **Hangfire Dashboard** | `http://localhost:5141/hangfire` | Kiểm tra Background Jobs (Sync) |

Khi khởi động lần đầu, app tự động:
1. Chạy EF Core Migration (tạo/cập nhật schema DB)
2. Seed dữ liệu mẫu (nếu DB trống)
3. Seed API data sources (Semantic Scholar, OpenAlex)
4. Đăng ký Hangfire job sync hàng ngày

---

## Danh sách tài khoản test

Đã được seed sẵn khi DB trống (`UserSeeder.cs`):

| Email | Password | Role |
|---|---|---|
| admin@gmail.com | Admin123! | Admin |
| thuan@gmail.com | Thuan123! | LecturerStudent |
| tien@gmail.com | Tien123! | LecturerStudent |
| lan@gmail.com | Lan123! | LecturerStudent |
| nam@gmail.com | Nam123! | LecturerStudent |

> Muốn test role **Researcher**: Admin đổi role qua `PATCH /api/admin/users/{id}/role` với body `{ "role": "Researcher" }`.

---

## Ví dụ test nhanh bằng curl

```bash
# 1. Login lấy token
curl -X POST http://localhost:5141/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin@gmail.com\",\"password\":\"Admin123!\"}"

# 2. Lấy dữ liệu Dashboard
curl http://localhost:5141/api/dashboard/overview \
  -H "Authorization: Bearer <TOKEN_OD_B1>"

# 3. Tìm kiếm bài báo
curl "http://localhost:5141/api/papers/search?query=learning&searchType=keyword" \
  -H "Authorization: Bearer <TOKEN_OD_B1>"

# 4. Export báo cáo CSV
curl "http://localhost:5141/api/reports/export/csv?groupBy=year" \
  -H "Authorization: Bearer <TOKEN>" \
  -o report.csv
```
