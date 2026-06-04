# 🎓 ScholarTrend — Kế Hoạch Triển Khai Dự Án

> **Scientific Journal Publication Trend Tracking System**  
> Dự án SWP391 — .NET 9 + React

---

## 1. Phân tích vấn đề & Giải pháp

### Vấn đề cốt lõi

| Vấn đề | Biểu hiện | Giải pháp |
|---|---|---|
| Quá nhiều bài báo, khó định hướng | Mất hàng giờ tìm paper phù hợp | Dashboard xu hướng trực quan theo thời gian |
| Tìm kiếm chỉ theo keyword | Không thấy bức tranh tổng thể lĩnh vực | Trend chart theo keyword/topic/journal |
| Không biết topic nào đang nổi | Không có gợi ý định hướng nghiên cứu | Trending Score + GrowthRate hiển thị realtime |
| Tốn thời gian theo dõi thủ công | Phải kiểm tra từng nguồn thủ công | Auto-sync từ Semantic Scholar / OpenAlex + Notification |

### Giải pháp tổng thể
Xây dựng nền tảng **3 trụ cột**:
1. **Search Engine nội bộ** — tìm kiếm nhanh từ DB local, fallback sang API ngoài nếu không có
2. **Trend Engine** — tính GrowthRate, TrendingScore theo tháng cho keyword/topic/journal
3. **Personalization** — mỗi user có dashboard riêng, bookmark, follow, nhận notification

---

## 2. Công nghệ sử dụng

### Backend

| Thành phần | Công nghệ | Version | Lý do |
|---|---|---|---|
| Framework | ASP.NET Core | 9.0 | LTS, performance cao, ecosystem mạnh |
| ORM | Entity Framework Core | 9.0 | Code-First, migration dễ, đã có sẵn trong project |
| Database | SQL Server (LocalDB dev / SQL Server prod) | 2022 | ACID, hỗ trợ tốt EF Core |
| Auth | ASP.NET Identity + JWT Bearer | - | Đã setup sẵn, chuẩn industry |
| Background Jobs | Hangfire | 1.8.x | Đã cài sẵn, dashboard monitoring, persistent jobs |
| HTTP Resilience | Polly | 8.x | Retry + Circuit Breaker khi gọi API ngoài |
| API Docs | Swagger / Scalar | - | Tài liệu API tự động |
| Caching | In-Memory Cache (IMemoryCache) | - | Cache trending data, giảm tải DB |
| Logging | Serilog | - | Structured logging, dễ debug |

### Frontend

| Thành phần | Công nghệ | Version | Lý do |
|---|---|---|---|
| Framework | React | 18.x | Ecosystem lớn, phổ biến, dễ tìm tài liệu |
| Build Tool | Vite | 5.x | Nhanh, HMR tốt |
| UI Library | Ant Design (antd) | 5.x | Component đầy đủ, có Chart, Table, Form sẵn |
| Charts | Recharts hoặc Chart.js | Latest | Dễ dùng với React, đẹp |
| State | React Query (TanStack Query) | 5.x | Server state management, caching, tự động refetch |
| Routing | React Router | 6.x | Tiêu chuẩn |
| HTTP Client | Axios | Latest | Interceptor JWT token dễ |
| Auth | JWT local storage + Axios interceptor | - | Đơn giản, phù hợp project học |

### External APIs (nguồn dữ liệu)

| API | Miễn phí | Dữ liệu | Khi nào dùng |
|---|---|---|---|
| **Semantic Scholar** | ✅ Có rate limit | Paper, Author, Citation | Phase 3 — ưu tiên đầu tiên |
| **OpenAlex** | ✅ Không giới hạn | Paper, Journal, Concept | Phase 3 — backup / bổ sung |
| **CrossRef** | ✅ | DOI, Journal metadata | Phase 3 — bổ sung DOI |

> [!TIP]
> Semantic Scholar có API key miễn phí nâng rate limit lên 100 req/s. Đăng ký tại https://www.semanticscholar.org/product/api

---

## 3. Cấu trúc dự án (Clean Architecture)

```
ScholarTrend/
├── ScholarTrend.Domain/          ← Entities, Enums, Interfaces thuần túy
│   ├── Entities/                 ✅ Đã có đủ 20 entities
│   └── Enums/                    ✅ PaperStatus, UserRole
│
├── ScholarTrend.Application/     ← Business Logic (CẦN XÂY DỰNG)
│   ├── Interfaces/
│   │   ├── Repositories/         ← IResearchPaperRepository, IKeywordRepository...
│   │   └── Services/             ← ISearchService, ITrendService, ISyncService...
│   ├── DTOs/                     ← Request/Response models
│   ├── Services/                 ← Triển khai business logic
│   └── Mappings/                 ← AutoMapper profiles
│
├── ScholarTrend.Infrastructure/  ← DB, External API, Hangfire
│   ├── Data/
│   │   ├── AppDbContext.cs       ✅ Đã có
│   │   ├── Configurations/       ✅ Đã có (5 file)
│   │   ├── Repositories/         ← Triển khai IRepository
│   │   └── Seeders/              ✅ Đã có dữ liệu mẫu
│   ├── ExternalApis/
│   │   ├── SemanticScholar/      ← HTTP client + model
│   │   └── OpenAlex/
│   ├── Jobs/                     ← Hangfire background jobs
│   └── Migrations/               ✅ Đã có
│
├── ScholarTrend.API/             ← Controllers, Middleware
│   ├── Controllers/              ← CẦN XÂY DỰNG
│   ├── Program.cs                ✅ Đã có
│   └── appsettings.json          ✅ Đã có
│
├── ScholarTrend.Tests/           ← Unit & Integration Tests
│
└── scholar-trend-ui/             ← React Frontend (TẠO MỚI)
    ├── src/
    │   ├── pages/
    │   ├── components/
    │   ├── services/             ← API calls
    │   └── hooks/
    └── vite.config.js
```

---

## 4. Lộ trình thực hiện theo Phase

---

### 🔵 Phase 1 — Foundation & Authentication
> **Thời gian ước tính: 1 tuần**

**Mục tiêu:** Hệ thống chạy được, user đăng nhập được, có Swagger.

#### Backend tasks
- [ ] Cài **AutoMapper**, **Serilog** vào project
- [ ] Tạo `Application` layer: interfaces repository + service cơ bản
- [ ] Implement `GenericRepository<T>` và repository cụ thể
- [ ] Tạo `AuthController`: Register, Login trả JWT, RefreshToken
- [ ] Đăng ký **Hangfire** vào [Program.cs](file:///c:/Users/Admin/Documents/Ky_8/SWP_391/ScholarTrend/ScholarTrend.API/Program.cs) (dashboard tại `/hangfire`)
- [ ] Setup **Swagger** với JWT authentication support

#### Frontend tasks
- [ ] Khởi tạo project React + Vite
- [ ] Layout chính: Sidebar, Navbar, Footer
- [ ] Trang Login / Register
- [ ] Axios instance với JWT interceptor (tự động gắn token vào header)
- [ ] Route guard (redirect về Login nếu chưa đăng nhập)

#### ✅ Có gì khi hoàn thành Phase 1
- User tạo tài khoản, đăng nhập, nhận JWT token
- Swagger UI test được tất cả API có xác thực
- Frontend có layout + trang login hoạt động
- Database tự migrate + seed dữ liệu mẫu khi khởi động
- Hangfire dashboard tại `/hangfire`

---

### 🟡 Phase 2 — Core Features (Search + Paper Detail + Bookmark)
> **Thời gian ước tính: 1.5–2 tuần**

**Mục tiêu:** User tìm được bài báo, xem chi tiết, lưu bookmark.

#### Backend tasks
- [ ] `PaperController`: Search (keyword/author/journal), GetById, GetList
- [ ] `BookmarkController`: Add, Remove, GetUserBookmarks
- [ ] `SearchHistoryController`: Tự động ghi log khi search
- [ ] `TopicController` + `JournalController`: GetAll, GetById
- [ ] `UserController`: GetProfile, UpdateProfile
- [ ] Phân quyền bằng `[Authorize(Roles = "...")]`

#### Frontend tasks
- [ ] Trang **Home/Dashboard** cơ bản (placeholder chart)
- [ ] Trang **Search**: thanh tìm kiếm, filter theo năm/journal, hiển thị kết quả dạng card
- [ ] Trang **Paper Detail**: full thông tin, danh sách tác giả, keyword, nút bookmark
- [ ] Trang **My Bookmarks**: danh sách bài đã lưu
- [ ] Trang **Profile**: thông tin user, edit profile

#### ✅ Có gì khi hoàn thành Phase 2
- **Demo được toàn bộ luồng cơ bản**: Đăng nhập → Tìm kiếm → Xem chi tiết → Bookmark
- Có thể phân biệt 3 role (Admin, Researcher, Lecturer/Student)
- Lịch sử tìm kiếm được ghi tự động
- Đủ để báo cáo tiến độ milestone giữa kỳ

---

### 🟠 Phase 3 — Trend Engine + External API Sync
> **Thời gian ước tính: 2 tuần**

**Mục tiêu:** Tính năng cốt lõi differentiator — phân tích xu hướng và đồng bộ dữ liệu ngoài.

#### Backend tasks
- [ ] `TrendController`: GetKeywordTrends, GetTopicTrends, GetJournalTrends (filter theo tháng/năm)
- [ ] `TrendCalculatorService`: Tính **GrowthRate** và **TrendingScore** từ dữ liệu bài báo
- [ ] `SemanticScholarClient`: HTTP client gọi Semantic Scholar API (dùng Polly retry)
- [ ] `SyncJob` (Hangfire CRON): Mỗi ngày tự động fetch paper mới, cập nhật trend
- [ ] `NotificationService`: Tạo notification khi có paper mới theo topic/journal user đang follow
- [ ] `FollowController`: Follow/Unfollow topic, journal
- [ ] `NotificationController`: GetNotifications, MarkAsRead

#### Frontend tasks
- [ ] Trang **Trending Dashboard**:
  - Biểu đồ line chart: số paper theo tháng (Recharts/Chart.js)
  - Top 10 keyword/topic đang tăng mạnh
  - Filter theo khoảng thời gian
- [ ] Trang **Topic/Journal Detail**: thông tin + trend chart riêng
- [ ] **Notification bell**: hiển thị số unread, dropdown danh sách
- [ ] Trang **Follow Management**: danh sách topic/journal đang follow

#### ✅ Có gì khi hoàn thành Phase 3
- Biểu đồ xu hướng thực tế hoạt động với dữ liệu thật
- Dữ liệu tự động cập nhật mỗi ngày qua Hangfire job
- User nhận được notification khi có paper mới
- Tính năng follow hoạt động
- **Đây là phần "wow factor" của đề tài — phân biệt với chỉ làm thư viện bài báo đơn thuần**

---

### 🟢 Phase 4 — Admin Panel + Polish + Report
> **Thời gian ước tính: 1 tuần**

**Mục tiêu:** Hoàn thiện, hệ thống production-ready để demo.

#### Backend tasks
- [ ] `AdminController`: Quản lý user (list, activate/deactivate, change role)
- [ ] `ApiDataSourceController`: Quản lý nguồn dữ liệu API
- [ ] `SyncLogController`: Xem lịch sử sync, status
- [ ] `ReportController`: Export báo cáo thống kê đơn giản (JSON/Excel)
- [ ] Caching trend data bằng `IMemoryCache` (cache 1 giờ)
- [ ] Rate limiting cơ bản
- [ ] Unit tests cho TrendCalculatorService và SearchService

#### Frontend tasks
- [ ] Trang **Admin Dashboard**: thống kê tổng quan (số user, số paper, số sync)
- [ ] Trang **User Management**: bảng list user, filter, action
- [ ] Trang **Sync Management**: kích hoạt sync thủ công, xem log
- [ ] **Responsive design** — đảm bảo dùng được trên tablet
- [ ] Loading states, error handling, empty states đẹp

#### ✅ Có gì khi hoàn thành Phase 4
- Hệ thống hoàn chỉnh end-to-end
- Admin quản lý được toàn bộ hệ thống
- Performance tốt hơn nhờ caching
- Có unit tests cơ bản
- **Sẵn sàng demo cho hội đồng**

---

## 5. Lưu ý khi triển khai

> [!IMPORTANT]
> **Về Application Layer (hiện đang rỗng):** Đây là việc cần làm đầu tiên trong Phase 1. Cần tạo các interfaces trước, rồi mới implement. Thứ tự: `IRepository` → `IService` → `ServiceImplementation` → `Controller`.

> [!NOTE]
> **Về External API:** Semantic Scholar cho phép 100 requests/5 giây với API key. Cần implement Queue hoặc delay giữa các request để không bị block.

> [!TIP]
> **Về Trend Score:** Công thức đề xuất đơn giản: `TrendingScore = (PaperCount_tháng_này / PaperCount_tháng_trước - 1) * 100`. Có thể tinh chỉnh thêm trọng số citation. Đây là "thuật toán" của riêng project.

> [!WARNING]
> **Về Deployment:** Nếu cần deploy cho demo, dùng SQL Server trên máy local hoặc Azure Free Tier. **Không commit connection string thật** vào git — dùng [appsettings.Development.json](file:///c:/Users/Admin/Documents/Ky_8/SWP_391/ScholarTrend/ScholarTrend.API/appsettings.Development.json) (đã có trong .gitignore).

---

## 6. Tóm tắt điểm mạnh của kiến trúc hiện tại

| Điều đã đúng | Lý do tốt |
|---|---|
| Clean Architecture 4 layer | Code dễ maintain, thay đổi DB/UI không ảnh hưởng business logic |
| `IEntityTypeConfiguration<T>` riêng file | Mỗi entity config độc lập, dễ đọc |
| Seeder theo thứ tự phụ thuộc | Đảm bảo FK constraint không bị lỗi |
| Index trên ExternalId, Title, CitationCount | Search nhanh kể cả khi có triệu bài báo |
| `PaperStatus` enum pipeline | Theo dõi được vòng đời paper từ fetch đến available |
| Hangfire + Polly đã cài sẵn | Architect đã nghĩ trước cho Phase 3 |
