# 2. Architecture & Design (Kiến trúc & Cấu trúc)

## Tổng quan kiến trúc (Clean Architecture)

Dự án chia thành 4 lớp chính theo Clean Architecture, đảm bảo Loose Coupling (liên kết lỏng lẻo) và nguyên tắc đảo ngược phụ thuộc (Dependency Inversion).

```
ScholarTrend.API            → Controllers, Middleware, DI, Swagger, Hangfire
ScholarTrend.Application    → Services, DTOs, Validators, Interfaces
ScholarTrend.Domain         → Entities, Enums, Constants (không phụ thuộc layer khác)
ScholarTrend.Infrastructure → EF Core DbContext, Repositories, External API clients, Seeders
```

**Luồng xử lý một request:**

```
Client → Controller → Service → Repository → SQL Server
                  ↓
              DTO / Validator
```

**Nguyên tắc phụ thuộc:** 
- Domain không phụ thuộc ai. 
- Application chỉ phụ thuộc Domain. 
- Infrastructure và API phụ thuộc Application. 
- Controller không truy cập DbContext trực tiếp — mọi logic nằm ở Service.

---

## Tại sao thiết kế như vậy?

### Clean Architecture (4 layer)
- **Tách biệt trách nhiệm:** Controller chỉ nhận request/trả response; Service chứa business logic; Repository chỉ truy vấn DB.
- **Dễ test & mở rộng:** Có thể thay SQL Server bằng DB khác mà không đụng Application layer.

### Repository + Unit of Work
- Mỗi entity có repository riêng (`ResearchPaperRepository`, `BookmarkRepository`, …) với query chuyên biệt (search, filter, include navigation).
- `UnitOfWork` gom các repository và quản lý transaction — tránh inject quá nhiều repository vào Service.

### FluentValidation
- Validate DTO tách khỏi Controller — rule tập trung, tái sử dụng, message lỗi nhất quán.
- Tự động trả 400 Bad Request khi validation fail.

### Trend Engine + Cache
- Dữ liệu trend tính sẵn và lưu bảng `KeywordTrend`, `TopicTrend`, `JournalTrend` — query nhanh cho biểu đồ.
- Dashboard tổng cache 1 giờ vì dữ liệu phân tích hệ thống không cần biến động từng giây.

---

## Cấu trúc thư mục chi tiết

```
Research-Analysis/
├── ScholarTrend.sln
├── ScholarTrend.API/
│   ├── Controllers/          # 14 controllers
│   ├── Program.cs            # DI, JWT, Hangfire, Migration, Seed
│   └── appsettings.json
├── ScholarTrend.Application/
│   ├── DTOs/                 # Request/Response objects
│   ├── Interfaces/           # Service & Repository contracts
│   ├── Services/             # Business logic (TrendService, SyncService...)
│   ├── Validators/           # FluentValidation rules
│   └── Mappings/             # Entity → DTO mappers
├── ScholarTrend.Domain/
│   ├── Entities/             # Models DB
│   ├── Enums/
│   └── Constants/
├── ScholarTrend.Infrastructure/
│   ├── Data/                 # DbContext, Migrations
│   ├── Data/Seeders/         # Dữ liệu mẫu khởi tạo
│   ├── Repositories/         # Unit of Work & các Repositories
│   ├── ExternalApis/         # SemanticScholarClient, OpenAlexClient
│   └── Jobs/                 # SyncJob (Hangfire)
└── ScholarTrend.Tests/       # xUnit Project (Unit Tests)
```
