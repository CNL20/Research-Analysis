# ScholarTrend

**Hệ thống theo dõi xu hướng công bố báo chí khoa học** (Scientific Journal Publication Trend Tracking System).

Dự án SWP — nhóm SU26SWP06. Backend REST API xây dựng trên **.NET 9**, kiến trúc **Clean Architecture**, cung cấp đầy đủ chức năng từ xác thực người dùng, tìm kiếm bài báo, phân tích xu hướng, cá nhân hóa, đồng bộ dữ liệu ngoài, đến dashboard và báo cáo.

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Công nghệ sử dụng](#2-công-nghệ-sử-dụng)
3. [Các module đã triển khai](#3-các-module-đã-triển-khai)
4. [Tại sao thiết kế như vậy?](#4-tại-sao-thiết-kế-như-vậy)
5. [Cấu trúc thư mục](#5-cấu-trúc-thư-mục)
6. [Hướng dẫn cài đặt & chạy](#6-hướng-dẫn-cài-đặt--chạy)
7. [Cấu hình môi trường](#7-cấu-hình-môi-trường)
8. [Dữ liệu mẫu — lấy từ đâu?](#8-dữ-liệu-mẫu--lấy-từ-đâu)
9. [Vai trò người dùng (Roles)](#9-vai-trò-người-dùng-roles)
10. [Định dạng phản hồi API](#10-định-dạng-phản-hồi-api)
11. [Xác thực JWT](#11-xác-thực-jwt)
12. [Tài liệu API đầy đủ](#12-tài-liệu-api-đầy-đủ)
13. [Tài khoản test](#13-tài-khoản-test)
14. [Hangfire & đồng bộ tự động](#14-hangfire--đồng-bộ-tự-động)

---

## 1. Tổng quan kiến trúc

Dự án chia thành 4 lớp chính theo Clean Architecture:

```
ScholarTrend.API            → Controllers, Middleware, DI, Swagger, Hangfire
ScholarTrend.Application    → Services, DTOs, Validators, Interfaces
ScholarTrend.Domain         → Entities, Enums, Constants (không phụ thuộc layer khác)
ScholarTrend.Infrastructure → EF Core DbContext, Repositories, External API clients, Seeders
```

Luồng xử lý một request:

```
Client → Controller → Service → Repository → SQL Server
                  ↓
              DTO / Validator
```

**Nguyên tắc phụ thuộc:** Domain không phụ thuộc ai. Application chỉ phụ thuộc Domain. Infrastructure và API phụ thuộc Application. Controller không truy cập DbContext trực tiếp — mọi logic nằm ở Service.

---

## 2. Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| Runtime | .NET 9 |
| Web framework | ASP.NET Core Web API |
| ORM | Entity Framework Core 9 + SQL Server |
| Xác thực | ASP.NET Identity + JWT Bearer |
| Validation | FluentValidation |
| Background jobs | Hangfire (SQL Server storage) |
| Cache | IMemoryCache (trend dashboard, TTL 1 giờ) |
| API docs | Swagger / OpenAPI |
| External APIs | Semantic Scholar, OpenAlex |

---

## 3. Các module đã triển khai

| Module | Tên | Trạng thái | Mô tả ngắn |
|---|---|---|---|
| **A** | Auth & User Management | ✅ | Đăng ký, đăng nhập, refresh token, profile, quản lý user (Admin) |
| **B** | Search & Paper | ✅ | Tìm kiếm bài báo, chi tiết, bookmark, topics, journals |
| **C** | Trend Engine | ✅ | Xu hướng keyword/topic/journal, top trending, so sánh |
| **D** | Personalization | ✅ | Follow topic/journal, thông báo, dashboard cá nhân |
| **E** | Data Sync & Admin | ✅ | Đồng bộ Semantic Scholar + OpenAlex, Hangfire job hàng ngày |
| **F** | Dashboard, Report & Admin | ✅ | Overview dashboard, admin dashboard, export báo cáo JSON/CSV |

---

## 4. Tại sao thiết kế như vậy?

### Clean Architecture (4 layer)

- **Tách biệt trách nhiệm:** Controller chỉ nhận request/trả response; Service chứa business logic; Repository chỉ truy vấn DB.
- **Dễ test & mở rộng:** Có thể thay SQL Server bằng DB khác mà không đụng Application layer.
- **Phù hợp yêu cầu SWP:** Dự án lớn, nhiều module — cần cấu trúc rõ ràng để nhiều thành viên làm song song.

### Repository + Unit of Work

- Mỗi entity có repository riêng (`ResearchPaperRepository`, `BookmarkRepository`, …) với query chuyên biệt (search, filter, include navigation).
- `UnitOfWork` gom các repository và quản lý transaction — tránh inject quá nhiều repository vào Service.

### JWT + Refresh Token

- **JWT (access token):** Stateless, phù hợp REST API, client (web/mobile) tự gửi kèm header.
- **Refresh token:** Lưu DB, rotate mỗi lần refresh — giảm rủi ro khi access token bị lộ, không cần user đăng nhập lại liên tục.

### FluentValidation

- Validate DTO tách khỏi Controller — rule tập trung, tái sử dụng, message lỗi nhất quán.
- Tự động trả 400 Bad Request khi validation fail.

### ApiResponse wrapper

- Mọi endpoint trả cùng format `{ success, message, data, errors }` — frontend xử lý thống nhất, không phải đoán cấu trúc từng API.

### Trend Engine + Cache

- Dữ liệu trend tính sẵn và lưu bảng `KeywordTrend`, `TopicTrend`, `JournalTrend` — query nhanh cho biểu đồ.
- Dashboard trend cache 1 giờ vì dữ liệu không cần real-time từng giây.

### Data Sync (Module E)

- Bài báo seed ban đầu là dữ liệu giả lập để demo. Production cần nguồn thật → tích hợp **Semantic Scholar** và **OpenAlex** (miễn phí, không cần API key).
- Hangfire chạy sync hàng ngày; Admin có thể trigger thủ công.
- Khi sync thêm paper mới → tự gửi notification cho user đang follow topic/journal liên quan.

### Role-based Authorization

- 3 role: `Admin`, `Researcher`, `LecturerStudent` — phân quyền theo nghiệp vụ (Admin quản trị, Researcher export báo cáo/so sánh trend, LecturerStudent dùng cơ bản).

---

## 5. Cấu trúc thư mục

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
│   ├── Services/             # Business logic
│   ├── Validators/           # FluentValidation rules
│   └── Mappings/             # Entity → DTO mappers
├── ScholarTrend.Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Constants/
├── ScholarTrend.Infrastructure/
│   ├── Data/                 # DbContext, Migrations
│   ├── Data/Seeders/         # Dữ liệu mẫu khởi tạo
│   ├── Repositories/
│   ├── ExternalApis/         # SemanticScholarClient, OpenAlexClient
│   └── Jobs/                 # SyncJob (Hangfire)
└── ScholarTrend.Tests/
```

---

## 6. Hướng dẫn cài đặt & chạy

### Yêu cầu

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB, Express, hoặc full instance)
- (Tuỳ chọn) Postman hoặc curl để test API

### Bước 1 — Clone & cấu hình

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

### Bước 2 — Chạy

```bash
dotnet run --project ScholarTrend.API --launch-profile http
```

### Bước 3 — Truy cập

| URL | Mô tả |
|---|---|
| http://localhost:5141/swagger | Swagger UI — test API |
| http://localhost:5141/hangfire | Hangfire Dashboard — xem background jobs |

Khi khởi động lần đầu, app tự động:
1. Chạy EF Core Migration (tạo/cập nhật schema DB)
2. Seed dữ liệu mẫu (nếu DB trống)
3. Seed API data sources (Semantic Scholar, OpenAlex)
4. Đăng ký Hangfire job sync hàng ngày

---

## 7. Cấu hình môi trường

### Connection String

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=ScholarTrendDb;Trusted_Connection=true;TrustServerCertificate=true;"
}
```

Thay `Server=.` bằng server SQL của bạn nếu cần.

### JWT

| Key | Mô tả | Mặc định |
|---|---|---|
| `Authentication:Jwt:SecretKey` | Khóa ký JWT (≥ 32 ký tự) | Bắt buộc |
| `Authentication:Jwt:ExpirationMinutes` | Thời hạn access token | 60 phút |
| `Authentication:Jwt:RefreshTokenExpirationDays` | Thời hạn refresh token | 7 ngày |

### External APIs (Sync)

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

Sync job dùng các query này để tìm và import bài báo mới từ API bên ngoài.

---

## 8. Dữ liệu mẫu — lấy từ đâu?

Dữ liệu ban đầu được seed **tự động khi app khởi động lần đầu** (DB trống). Logic nằm tại `ScholarTrend.Infrastructure/Data/Seeders/`.

### Cơ chế seed

```
Program.cs
  └── DatabaseSeeder.SeedAsync()        ← Chỉ chạy nếu DB chưa có dữ liệu
  └── ApiDataSourceSeeder.SeedAsync()   ← Luôn kiểm tra, seed nếu chưa có data source
```

`DatabaseSeeder` kiểm tra: nếu đã có Users, Journals, Authors, Keywords, Topics hoặc Papers → **bỏ qua** (không ghi đè).

### Bảng dữ liệu seed

| Seeder | File | Nội dung |
|---|---|---|
| `RoleSeeder` | `RoleSeeder.cs` | 3 role: Admin, Researcher, LecturerStudent |
| `UserSeeder` | `UserSeeder.cs` | 5 tài khoản test (xem [mục 13](#13-tài-khoản-test)) |
| `JournalSeeder` | `JournalSeeder.cs` | 5 tạp chí: Nature, Science, IEEE Access, ACM Computing Surveys, AI Journal |
| `AuthorSeeder` | `AuthorSeeder.cs` | 10 tác giả (VN, SG, US, CN, IN, …) |
| `KeywordSeeder` | `KeywordSeeder.cs` | 10 keyword: AI, ML, Deep Learning, NLP, Blockchain, … |
| `ResearchTopicSeeder` | `ResearchTopicSeeder.cs` | 5 chủ đề: AI, Data Science, Software Engineering, Cyber Security, Cloud Computing |
| `ResearchPaperSeeder` | `ResearchPaperSeeder.cs` | **20 bài báo giả lập** (title, abstract, DOI, citation, liên kết author/keyword/topic/journal) |
| `TrendSeeder` | `TrendSeeder.cs` | Trend data 12 tháng (06/2025–05/2026) tính từ papers seed |
| `ApiDataSourceSeeder` | `ApiDataSourceSeeder.cs` | 2 nguồn: SemanticScholar, OpenAlex |

### Dữ liệu thật từ bên ngoài

Sau khi seed, Admin có thể **đồng bộ thêm bài báo thật** qua:

- `POST /api/admin/sync/trigger` — gọi Semantic Scholar & OpenAlex API
- Hangfire job `daily-paper-sync` — chạy tự động mỗi ngày

Papers import từ API ngoài được lưu vào DB cùng bảng `ResearchPapers`, tự map keyword/topic/journal nếu khớp.

### Trend data

- **Seed:** `TrendSeeder` tính `PaperCount`, `CitationCount`, `GrowthRate`, `TrendingScore` theo từng tháng dựa trên ngày publish của papers seed.
- **Runtime:** Trend API đọc từ bảng `KeywordTrends`, `TopicTrends`, `JournalTrends` — không tính realtime mỗi request.

---

## 9. Vai trò người dùng (Roles)

| Role | Quyền |
|---|---|
| **Admin** | Toàn quyền: quản lý user, sync data, admin dashboard |
| **Researcher** | Tất cả chức năng user + export báo cáo + so sánh trend |
| **LecturerStudent** | Tìm kiếm, bookmark, follow, notification, dashboard cá nhân/tổng quan |

User đăng ký mới (`POST /api/auth/register`) mặc định role **LecturerStudent**.

---

## 10. Định dạng phản hồi API

Mọi endpoint trả về wrapper `ApiResponse<T>`:

```json
{
  "success": true,
  "message": "Success",
  "data": { ... },
  "errors": null
}
```

Khi lỗi:

```json
{
  "success": false,
  "message": "Topic not found.",
  "data": null,
  "errors": null
}
```

Validation lỗi (400):

```json
{
  "success": false,
  "message": "Validation failed.",
  "data": null,
  "errors": ["Email is required.", "Password must be at least 6 characters."]
}
```

---

## 11. Xác thực JWT

### Luồng sử dụng

1. **Đăng ký hoặc đăng nhập** → nhận `token` (access) + `refreshToken`
2. Gửi access token trong header mọi request cần auth:

```
Authorization: Bearer <token>
```

3. Khi access token hết hạn → gọi `POST /api/auth/refresh-token` với refresh token cũ → nhận cặp token mới

### Trên Swagger

1. Gọi `POST /api/auth/login`
2. Copy giá trị `data.token`
3. Bấm **Authorize** (góc trên Swagger) → nhập: `Bearer <token>`
4. Gọi các API còn lại

---

## 12. Tài liệu API đầy đủ

**Base URL:** `http://localhost:5141`

**Ký hiệu quyền:**
- 🔓 Public — không cần token
- 🔒 Auth — cần JWT (mọi role)
- 🔬 Researcher — Admin hoặc Researcher
- 👑 Admin — chỉ Admin

---

### Module A — Auth & User Management

#### 🔓 `POST /api/auth/register`

Đăng ký tài khoản mới. Role mặc định: `LecturerStudent`.

**Body:**
```json
{
  "fullName": "Nguyen Van A",
  "email": "a@gmail.com",
  "password": "Abc123!",
  "confirmPassword": "Abc123!",
  "institution": "HCMUT",
  "researchField": "AI"
}
```

**Response `data`:** `AuthResponse` (token, refreshToken, userId, email, roles, …)

---

#### 🔓 `POST /api/auth/login`

**Body:**
```json
{
  "email": "admin@gmail.com",
  "password": "Admin123!"
}
```

---

#### 🔓 `POST /api/auth/refresh-token`

**Body:**
```json
{
  "refreshToken": "<refresh-token-từ-login>"
}
```

---

#### 🔒 `GET /api/auth/profile`

Lấy profile user hiện tại.

---

#### 🔒 `PUT /api/auth/profile`

**Body:**
```json
{
  "fullName": "Ten Moi",
  "institution": "Truong ABC",
  "researchField": "Data Science"
}
```

---

#### 👑 `GET /api/admin/users`

Danh sách user. Query params:

| Param | Kiểu | Mô tả |
|---|---|---|
| `search` | string | Tìm theo tên/email |
| `role` | string | Lọc role: Admin, Researcher, LecturerStudent |
| `isActive` | bool | Lọc trạng thái active |

---

#### 👑 `GET /api/admin/users/{id}`

Chi tiết một user.

---

#### 👑 `PATCH /api/admin/users/{id}/status`

Kích hoạt/vô hiệu hóa user.

**Body:**
```json
{ "isActive": false }
```

---

#### 👑 `PATCH /api/admin/users/{id}/role`

Đổi role user.

**Body:**
```json
{ "role": "Researcher" }
```

---

### Module B — Search & Paper

> Tất cả endpoint dưới đây cần 🔒 Auth.

#### `GET /api/papers/search`

Tìm kiếm bài báo. Tự lưu lịch sử tìm kiếm.

| Param | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `query` | string | — | Từ khóa tìm |
| `searchType` | string | `keyword` | `keyword` \| `author` \| `journal` \| `all` |
| `journalId` | int | — | Lọc theo journal |
| `topicId` | int | — | Lọc theo topic |
| `yearFrom` | int | — | Năm publish từ |
| `yearTo` | int | — | Năm publish đến |
| `minCitations` | int | — | Số citation tối thiểu |
| `page` | int | 1 | Trang |
| `pageSize` | int | 10 | Số item/trang |

**Ví dụ:**
```
GET /api/papers/search?query=transformer&searchType=keyword&yearFrom=2025&page=1&pageSize=10
```

---

#### `GET /api/papers/{id}`

Chi tiết bài báo (authors, keywords, topics, journal, isBookmarked).

---

#### `GET /api/papers/by-topic/{topicId}`

Bài báo theo chủ đề. Query: `page`, `pageSize`.

---

#### `GET /api/papers/by-journal/{journalId}`

Bài báo theo tạp chí. Query: `page`, `pageSize`.

---

#### `GET /api/papers/search-history`

Lịch sử tìm kiếm của user. Query: `limit` (mặc định 20).

---

#### `GET /api/bookmarks`

Danh sách bookmark của user.

---

#### `POST /api/bookmarks/{paperId}`

Bookmark một bài báo.

---

#### `DELETE /api/bookmarks/{paperId}`

Bỏ bookmark.

---

#### `GET /api/topics`

Danh sách tất cả research topics (kèm paperCount).

---

#### `GET /api/topics/{id}`

Chi tiết topic: mô tả, số paper, 5 paper gần nhất, **trendChart** (biểu đồ xu hướng).

---

#### `GET /api/journals`

Danh sách tạp chí.

---

#### `GET /api/journals/{id}`

Chi tiết journal: publisher, ISSN, impact factor, recent papers, **trendChart**.

---

### Module C — Trend Engine

> Tất cả cần 🔒 Auth.

**Query params chung (`TrendFilterRequest`):**

| Param | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `yearFrom` | int | 2025 | Năm bắt đầu |
| `yearTo` | int | 2026 | Năm kết thúc |
| `monthFrom` | int | — | Tháng bắt đầu |
| `monthTo` | int | — | Tháng kết thúc |
| `keywordId` | int | — | Lọc 1 keyword |
| `topicId` | int | — | Lọc 1 topic |
| `journalId` | int | — | Lọc 1 journal |
| `top` | int | 10 | Số item top (1–50) |

#### `GET /api/trends/dashboard`

Dashboard trend tổng hợp: top keywords, top topics, top journals, publication trend. **Cache 1 giờ.**

---

#### `GET /api/trends/keywords`

Time-series trend theo keyword (cho line chart).

---

#### `GET /api/trends/keywords/top`

Top keyword trending theo TrendingScore kỳ mới nhất.

---

#### `GET /api/trends/topics`

Time-series trend theo research topic.

---

#### `GET /api/trends/topics/top`

Top topic trending.

---

#### `GET /api/trends/journals`

Time-series trend theo journal.

---

#### `GET /api/trends/journals/top`

Top journal trending.

---

#### `GET /api/trends/publications`

Xu hướng công bố tổng thể (aggregate tất cả papers).

---

#### 🔬 `POST /api/trends/compare`

So sánh 2–3 keyword/topic/journal trên cùng biểu đồ.

**Body:**
```json
{
  "type": "topic",
  "ids": [1, 2, 3],
  "filter": {
    "yearFrom": 2025,
    "yearTo": 2026
  }
}
```

`type`: `keyword` | `topic` | `journal`

---

### Module D — Personalization

#### `GET /api/follows/topics`

Danh sách topic đang follow.

#### `GET /api/follows/journals`

Danh sách journal đang follow.

#### `POST /api/follows/topics/{topicId}`

Follow một topic.

#### `DELETE /api/follows/topics/{topicId}`

Unfollow topic.

#### `POST /api/follows/journals/{journalId}`

Follow journal.

#### `DELETE /api/follows/journals/{journalId}`

Unfollow journal.

---

#### `GET /api/notifications`

Thông báo của user.

| Param | Kiểu | Mô tả |
|---|---|---|
| `isRead` | bool | `true` = đã đọc, `false` = chưa đọc, bỏ trống = tất cả |
| `limit` | int | Số lượng (mặc định 20) |

---

#### `GET /api/notifications/unread-count`

Số thông báo chưa đọc. Response: `{ "count": 3 }`

---

#### `PATCH /api/notifications/{id}/read`

Đánh dấu 1 thông báo đã đọc.

---

#### `PATCH /api/notifications/read-all`

Đánh dấu tất cả đã đọc.

---

#### `GET /api/notifications/settings`

Cài đặt thông báo.

---

#### `PUT /api/notifications/settings`

**Body:**
```json
{
  "emailEnabled": true,
  "topicAlertEnabled": true,
  "frequency": "Daily"
}
```

`frequency`: `Daily` | `Weekly` | `Instant`

---

#### `GET /api/dashboard/personal`

Dashboard cá nhân: bookmark count, follow count, unread notifications, recent bookmarks, followed topics/journals, recommended topics.

---

### Module E — Data Sync & Admin

#### 👑 `POST /api/admin/sync/trigger`

Kích hoạt đồng bộ dữ liệu từ API ngoài.

**Body (tuỳ chọn):**
```json
{ "sourceName": "SemanticScholar" }
```

Bỏ trống body → sync tất cả nguồn active. `sourceName`: `SemanticScholar` | `OpenAlex`

---

#### 👑 `GET /api/admin/sync/logs`

Lịch sử sync. Query: `limit` (mặc định 50).

---

#### 👑 `GET /api/admin/sync/data-sources`

Danh sách nguồn API (Semantic Scholar, OpenAlex).

---

#### 👑 `PATCH /api/admin/sync/data-sources/{id}`

Bật/tắt nguồn sync.

**Body:**
```json
{ "isActive": true }
```

---

### Module F — Dashboard, Report & Admin

#### `GET /api/dashboard/overview`

Dashboard tổng quan hệ thống (mọi user đã đăng nhập): total papers/keywords/topics/journals/authors, publication trend, top keywords, top topics.

---

#### 👑 `GET /api/admin/dashboard`

Dashboard Admin: thống kê user (total/active/by role), papers, bookmarks, follows, sync logs, data sources, publication trend, top keywords.

---

#### 🔬 `GET /api/reports/publications`

Báo cáo công bố dạng JSON.

| Param | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `yearFrom` | int | 2020 | Năm bắt đầu |
| `yearTo` | int | năm hiện tại | Năm kết thúc |
| `groupBy` | string | `year` | `year` \| `keyword` \| `topic` |

**Ví dụ:**
```
GET /api/reports/publications?groupBy=keyword&yearFrom=2025&yearTo=2026
```

---

#### 🔬 `GET /api/reports/export/json`

Tải file JSON báo cáo. Cùng query params với `/publications`.

---

#### 🔬 `GET /api/reports/export/csv`

Tải file CSV báo cáo. Cùng query params với `/publications`.

---

## 13. Tài khoản test

Seed sẵn khi DB trống (`UserSeeder.cs`):

| Email | Password | Role |
|---|---|---|
| admin@gmail.com | Admin123! | Admin |
| thuan@gmail.com | Thuan123! | LecturerStudent |
| tien@gmail.com | Tien123! | LecturerStudent |
| lan@gmail.com | Lan123! | LecturerStudent |
| nam@gmail.com | Nam123! | LecturerStudent |

> Muốn test role **Researcher**: Admin đổi role qua `PATCH /api/admin/users/{id}/role` với body `{ "role": "Researcher" }`.

---

## 14. Hangfire & đồng bộ tự động

- **Dashboard:** http://localhost:5141/hangfire
- **Job:** `daily-paper-sync` — chạy `Cron.Daily` (mỗi ngày 1 lần)
- **Luồng sync:**
  1. Lấy danh sách `ApiDataSource` đang active
  2. Gọi Semantic Scholar / OpenAlex (HTTP client, retry 3 lần)
  3. Import paper mới vào DB (bỏ qua trùng DOI)
  4. Gửi notification cho user follow topic/journal liên quan
  5. Ghi log vào bảng `SyncLogs`

---

## Ví dụ test nhanh bằng curl

```bash
# 1. Login
curl -X POST http://localhost:5141/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin@gmail.com\",\"password\":\"Admin123!\"}"

# 2. Copy token, gọi API (thay <TOKEN>)
curl http://localhost:5141/api/dashboard/overview \
  -H "Authorization: Bearer <TOKEN>"

# 3. Tìm kiếm bài báo
curl "http://localhost:5141/api/papers/search?query=learning&searchType=keyword" \
  -H "Authorization: Bearer <TOKEN>"

# 4. Export báo cáo CSV
curl "http://localhost:5141/api/reports/export/csv?groupBy=year" \
  -H "Authorization: Bearer <TOKEN>" \
  -o report.csv
```

---

## Build & Test

```bash
# Build toàn solution
dotnet build ScholarTrend.sln

# Chạy tests (nếu có)
dotnet test ScholarTrend.Tests
```

---

## Tóm tắt luồng nghiệp vụ chính

```
User đăng nhập
    → Tìm kiếm / xem paper / bookmark
    → Follow topic/journal quan tâm
    → Xem trend dashboard & biểu đồ
    → Nhận notification khi có paper mới (từ sync)

Researcher
    → Export báo cáo JSON/CSV
    → So sánh trend nhiều keyword/topic/journal

Admin
    → Quản lý user (active/role)
    → Trigger sync / xem sync logs
    → Xem admin dashboard thống kê hệ thống
```

---

*ScholarTrend — SU26SWP06 · .NET 9 · Clean Architecture*
