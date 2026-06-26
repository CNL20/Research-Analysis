# 3. Business Logic (Nghiệp vụ & Chức năng)

Tài liệu này mô tả cách luồng nghiệp vụ của hệ thống ScholarTrend hoạt động.

---

## Tóm tắt luồng nghiệp vụ chính

```text
1. User đăng nhập
    → Tìm kiếm / xem paper / bookmark
    → Follow topic/journal quan tâm
    → Xem trend dashboard & biểu đồ
    → Nhận notification khi có paper mới (từ sync job)

2. Researcher
    → Mọi quyền của User
    → Kéo dữ liệu nâng cao
    → Export báo cáo JSON/CSV
    → So sánh trend nhiều keyword/topic/journal trên một trục tung

3. Admin
    → Quản lý user (Kích hoạt/Khoá/Đổi Role)
    → Trigger Data Sync thủ công 
    → Giám sát tiến trình Background job
    → Xem admin dashboard thống kê hệ thống (toàn cục)
```

---

## Các Module Nghiệp vụ Đã Triển khai

| Tên Module | Trạng thái | Mô tả cốt lõi |
|---|---|---|
| **Auth & User Management** | ✅ | Authorization JWT, Refresh token, Phân quyền Role, Cập nhật Profile. |
| **Search & Paper** | ✅ | Engine tìm giấy, filter chuyên sâu theo Domain, đánh dấu cá nhân (Bookmark). |
| **Trend Engine** | ✅ | **[CORE]** Tính điểm `GrowthRate` & `TrendingScore` định kỳ. Ranking keyword/topic. |
| **Personalization** | ✅ | Chức năng Following của user, Notification System, cá nhân hoá Dashboard. |
| **Data Sync** | ✅ | Đồng bộ nguồn API ngoài tự động. Tự mapping Keyword/Topic nếu bài báo mới xuất hiện. |
| **Report & Admin** | ✅ | Export dữ liệu báo cáo chuyên sâu csv/json để Analyst nghiên cứu. Quản trị hệ thống. |

---

## Vai trò người dùng (Roles)

| Role | Khả năng tiếp cận Hệ thống |
|---|---|
| **Admin** | Toàn quyền: quản lý user, sync data, admin dashboard |
| **Researcher** | Tất cả chức năng user + export báo cáo + truy cập API so sánh trend `POST /api/trends/compare` |
| **LecturerStudent** | Tìm kiếm, bookmark, follow, notification, dashboard cá nhân/tổng quan (Mặc định khi đăng ký mới) |

---

## Cơ chế Dữ liệu (Seeding & Sync)

### 1. Dữ liệu mẫu khởi tạo (DB trống)
Khi DB chưa có data, Engine sẽ boot `DatabaseSeeder` & `ApiDataSourceSeeder` tạo ra:
- **5 Tài khoản test** (Admin, Student,...)
- **5 Tạp chí (Journals)** & **10 Tác giả (Authors)** & **10 Keywords**
- **20 Bài Báo (ResearchPapers)** giả lập (có abstract, citation rank đầy đủ)
- Mẫu lịch sử Growth của 1 năm (`TrendSeeder.cs`).

### 2. Dữ liệu thật từ System ngoài (Data Sync & Hangfire)
Production cần kết nối thật -> Tích hợp **Semantic Scholar** và **OpenAlex** (Không cần API key).

**Cơ chế Background (Hangfire)**
- **Dashboard:** `http://localhost:5141/hangfire`
- **Job:** `daily-paper-sync` — chạy `Cron.Daily` (mỗi ngày 1 lần vào nửa đêm).
- **Luồng hoạt động của SyncJob:**
  1. Lấy danh sách nguồn dữ liệu `ApiDataSource` đang active
  2. Gửi HTTP Request tới Semantic Scholar / OpenAlex. (Tích hợp Resilience Polly retry 3 lần nếu sập mạng).
  3. Import bài báo vào DB, kiểm tra trùng bằng logic Duplicate check (`ExternalId`).
  4. Mapping các Metadata, nếu phát hiện có người đang `Follow` topic của bài báo này -> Tạo Notification đẩy về user đó.
  5. Đổ lịch sử đồng bộ vào `SyncLogs`.
