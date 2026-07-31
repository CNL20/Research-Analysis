# Demo Script — ScholarTrend

> Tài liệu này được tạo bằng cách đọc toàn bộ source code Backend và Frontend.
> Chỉ mô tả các chức năng **thực sự tồn tại trong code**.

---

## Mục lục

1. [Giới thiệu hệ thống](#1-giới-thiệu-hệ-thống)
2. [Chuẩn bị trước khi Demo](#2-chuẩn-bị-trước-khi-demo)
3. [Kịch bản Demo tổng quan](#3-kịch-bản-demo-tổng-quan)
4. [Authentication](#4-authentication)
5. [Student (LecturerStudent)](#5-student-lecturerstudent)
6. [Researcher](#6-researcher)
7. [Admin](#7-admin)
8. [Bảng phân quyền](#8-bảng-phân-quyền)
9. [Các trường hợp lỗi cần Demo](#9-các-trường-hợp-lỗi-cần-demo)
10. [Checklist Demo](#10-checklist-demo)
11. [Kịch bản Demo hoàn chỉnh (MC Script)](#11-kịch-bản-demo-hoàn-chỉnh-mc-script)

---

## 1. Giới thiệu hệ thống

### Mục đích hệ thống

**ScholarTrend** là nền tảng phân tích xu hướng nghiên cứu khoa học học thuật, cho phép người dùng:
- Tìm kiếm và khám phá các bài báo khoa học từ nhiều nguồn (SemanticScholar, OpenAlex, Crossref, ArXiv).
- Xem phân tích xu hướng (Trend) theo Topic, Keyword, Journal theo thời gian.
- Đọc phân tích **khoảng trống nghiên cứu (Research Gap)** do AI (Groq/Gemini) tự động sinh ra dựa trên nội dung bài báo thực tế.
- Theo dõi (Follow) tác giả, chủ đề, tạp chí yêu thích để nhận thông báo.
- Mua gói Premium để mở khóa tính năng phân tích AI nâng cao.

### Các Role

| Role | Tên hiển thị | Mô tả |
|------|------|------|
| `LecturerStudent` | Student / Lecturer | Role mặc định khi đăng ký. |
| `Researcher` | Researcher | Role nâng cao sau khi mua gói Premium. Xem Research Gap, phân tích AI. |
| `Admin` | Admin | Quản trị toàn bộ hệ thống. |

### Các chức năng chính

**Tất cả người dùng (kể cả chưa đăng nhập):**
- Xem Landing Page, tìm kiếm bài báo, tác giả
- Xem chi tiết bài báo, tác giả, tạp chí, Topic, Trend
- Xem trang Pricing

**Student (đăng nhập):** Dashboard, Bookmark, Follow, Notifications, Profile, Upload Documents, Mua Subscription

**Researcher (thêm):** Xem Research Gap, Gap Detail, Evidence, Pattern Mining, Trigger Regenerate Gaps

**Admin (thêm):** Quản lý User, Crawl data, Duyệt bài, Sync Schedule, Gap Analysis Pipeline, PDF Management

### Luồng tổng quan

```
Guest → Tìm kiếm / Xem Trend → Đăng ký → Verify Email → Student
                                                              ↓ Mua gói Premium
                                                         Researcher (xem Gap)

Admin: Crawl → Pending Review → Approve → AI Pipeline → Research Gaps
```

---

## 2. Chuẩn bị trước khi Demo

### Database
- PostgreSQL local: `ScholarTrend_Data`
- `appsettings.Development.json`: `Host=localhost;Port=5432;Database=ScholarTrend_Data;Username=postgres;Password=...`
- Đảm bảo có Paper, Topic, Author, Research Gap trong DB.

### Tài khoản từng role

| Role | Email | Mật khẩu |
|------|------|------|
| Admin | `admin@gmail.com` | `Admin123!` |
| LecturerStudent | `thuan@gmail.com` | `Thuan123!` |
| LecturerStudent | `tien@gmail.com` | `Tien123!` |

> **Lưu ý:** Không có tài khoản Researcher mặc định. Cần vào **Admin > User Management** đổi role trước khi demo.

### Dữ liệu mẫu cần kiểm tra
- [ ] Ít nhất 5 Research Topics có Papers
- [ ] Ít nhất 1 Topic đã chạy Pipeline và có Research Gaps
- [ ] Có Pending Sync Papers trong queue
- [ ] Có ít nhất 2 gói Subscription đang Active

### Chạy hệ thống

**Backend:**
- Start `ScholarTrend.API` từ Visual Studio → Port: `http://localhost:5142`
- Swagger: `http://localhost:5142/swagger`
- Hangfire: `http://localhost:5142/hangfire` (admin / 123456)

**Frontend:**
```bash
cd FE_ScholarTrend
npm run dev
```
→ `http://localhost:5173`

### Lưu ý quan trọng

> [!WARNING]
> **Research Gap Analysis** yêu cầu Groq AI API Key hoạt động. Kiểm tra `appsettings.Development.json` trước.

> [!NOTE]
> **Payment (PayOS)** cần ngrok để test local: `ngrok http 5142` → Cập nhật Webhook URL trên PayOS dashboard.

> [!IMPORTANT]
> Trước khi demo Researcher: Vào Admin > Users > Đổi role user lên Researcher > Logout và Login lại.

---

## 3. Kịch bản Demo tổng quan

```
Bắt đầu
  ↓
Authentication (Register → Verify → Login)
  ↓
Student: Dashboard → Search → Paper Detail → Bookmark → Topic (bị chặn Gap) → Pricing
  ↓
Researcher: Topic Detail (thấy Gap) → Gap Detail Modal → Regenerate Gaps
  ↓
Admin: Dashboard → User Management → Sync/Crawl → Approve Papers → Gap Pipeline
  ↓
Kết thúc
```

---

## 4. Authentication

### 4.1. Register thành công

**Mục tiêu:** Tạo tài khoản mới với role `LecturerStudent`.

**Các bước:**
1. Vào `/register`
2. Điền: Full Name, Email, Password, Confirm Password, Institution, Research Field
3. Bấm **Register**

**Kết quả:** Server gửi email xác thực. Chuyển hướng thông báo kiểm tra email.

**Lỗi:** Email đã tồn tại → "Email already registered." / Password không khớp → Lỗi client.

---

### 4.2. Xác thực Email

**Các bước:** Mở email → Click link → Vào `/verify-email?email=...&token=...`

**Kết quả:** "Email verified successfully." → Có thể đăng nhập.

**Lỗi:** Token hết hạn → Hiển thị nút "Resend Verification Email".

---

### 4.3. Login thành công

**Các bước:** `/login` → Nhập Email + Password → **Sign In**

**Kết quả:** Token lưu vào localStorage → Redirect `/dashboard` → Header hiển thị tên.

---

### 4.4. Login sai mật khẩu

**Demo:** Nhập đúng email, sai password.

**Kết quả:** Hiển thị thông báo lỗi từ Backend.

---

### 4.5. Login tài khoản chưa xác thực

**Demo:** Login với tài khoản vừa đăng ký, chưa click link email.

**Kết quả:** Thông báo email chưa được xác thực.

---

### 4.6. Google Login

**Các bước:** Trang Login → Bấm **Continue with Google** → Chọn tài khoản

**Kết quả:** Google trả `id_token` → Backend xác thực → Lưu token → Redirect Dashboard.

---

### 4.7. Quên mật khẩu

**Các bước:** `/forgot-password` → Nhập Email → **Send Reset Link**

**Kết quả:** Email chứa link reset được gửi.

---

### 4.8. Reset Password

**Các bước:** Click link email → `/reset-password?email=...&token=...` → Nhập mật khẩu mới

**Kết quả:** "Password reset successfully." → Đăng nhập được bằng mật khẩu mới.

---

### 4.9. Logout

**Các bước:** Click Avatar trên Header → **Log Out**

**Kết quả:** Xóa token localStorage → Redirect trang chủ.

---

### 4.10. Refresh Token (tự động)

**Cơ chế:** Khi API trả `401`, `api.js` tự gọi `POST /api/auth/refresh-token` và retry.

**Demo:** Chỉnh `ExpirationMinutes = 1` → Chờ hết hạn → Làm 1 thao tác → Xem Network tab thấy refresh-token call.

---

## 5. Student (LecturerStudent)

> Demo với tài khoản `thuan@gmail.com` / `Thuan123!`

### 5.1. Dashboard

**Bước:** Đăng nhập → `/dashboard`

**Kết quả:** Tổng quan Bookmark, Follow, thông báo, xu hướng mới nhất.

---

### 5.2. Search Paper

**Bước:** Nhập keyword (VD: "deep learning") → Enter → `/search/results`

**Kết quả:** Danh sách bài báo phân trang. Lọc theo Topic, Author, Journal, Year, Sort.

**Lỗi:** Không tìm thấy → "No results found."

---

### 5.3. Paper Detail

**Bước:** Click tên bài báo → `/papers/:paperId`

**Kết quả:**
- Tiêu đề, Abstract, Tác giả, DOI, Journal, Năm, Citations.
- View count tự động tăng.
- Nút Bookmark.
- Link download PDF (nếu có).
- **Nút "Analyze with AI" — chỉ Researcher/Admin mới dùng được** (Student thấy bị chặn).

---

### 5.4. Author Detail

**Bước:** `/authors` → Click tác giả → `/authors/id/:authorId`

**Kết quả:** Thông tin tác giả, danh sách bài báo, citations. Nút Follow/Unfollow.

---

### 5.5. Topics & Topic Detail

**Bước:** Menu **Topics** → `/topics` → Click vào 1 Topic → `/topics/:topicId`

**Kết quả trên Topic Detail:**
- Tiêu đề, mô tả, Stats (papers, citations, growth, peak score).
- Biểu đồ xu hướng (Line Chart: Papers + Citations theo thời gian).
- **Research Gaps (Student):** Thông báo `⭐ Premium Feature` — KHÔNG thấy nội dung Gap.
- Nút Follow/Unfollow Topic.
- 5 Recent Papers gần nhất.

---

### 5.6. Bookmark

**Bước:** Paper Detail → Click **Bookmark** → Vào `/bookmarks`

**Lỗi:** Chưa đăng nhập → Redirect Login.

---

### 5.7. Trends Chart

**Bước:** Menu **Trends** → `/trends`

**Kết quả:** Filter Topics/Keywords/Journals, biểu đồ so sánh xu hướng.

---

### 5.8. Following

**Bước:** `/following` hoặc `/profile` → Tab **Following**

**Kết quả:** Danh sách Topics, Journals, Authors, Papers đã Follow với nút Unfollow.

---

### 5.9. Notifications

**Bước:** Click chuông Header → `/notifications`

**Kết quả:** Danh sách thông báo, đánh dấu đã đọc.

---

### 5.10. Profile Management

**Bước:** `/profile`

**Chức năng:**
- Tab **Profile:** Cập nhật Full Name, Institution, Research Field → Save. Đổi mật khẩu. Upload/Remove avatar (JPG/PNG/WebP, max 5MB). Xem Current Plan + ngày hết hạn.
- Tab **Following:** Danh sách đã Follow.
- Tab **Documents:** Upload, xem, xóa file PDF cá nhân.

---

### 5.11. Pricing & Subscription

**Bước:**
1. `/pricing` → Xem gói (chỉ gói `isActive = true` hiện)
2. Click **Subscribe Now** → Chuyển PayOS checkout
3. Thanh toán (QR/chuyển khoản) → Webhook nhận → Redirect `/payment/result?status=success`

**Kết quả:** Role nâng lên `Researcher`. Profile hiển thị tên gói và ngày hết hạn.

---

### 5.12. Payment History

**Bước:** `/payment/history`

**Kết quả:** Danh sách giao dịch với trạng thái.

---

### Student KHÔNG được phép:
- Xem Research Gap → 403 Forbidden
- Truy cập `/admin/*` → Redirect `/dashboard`
- Phân tích AI bài báo

---

## 6. Researcher

> Đăng nhập tài khoản đã được đổi role lên Researcher.

### Researcher có thêm quyền:

| Tính năng | Student | Researcher |
|-----------|---------|------------|
| Xem Research Gap | ❌ | ✅ |
| Xem Gap Detail Modal | ❌ | ✅ |
| Xem Evidence / Timeline / Pattern | ❌ | ✅ |
| Trigger Regenerate Gaps | ❌ | ✅ |
| Phân tích AI bài báo từ PDF | ❌ | ✅ (Backend có, UI chưa tích hợp nút rõ ràng) |

---

### 6.1. Topic Detail - Research Gap (Researcher)

**Bước:** Đăng nhập Researcher → `/topics/:topicId`

**Kết quả (thay vì bị chặn):**
- **"Generated gap analysis" section:**
  - Thời gian generate lần cuối.
  - Coverage label: "X / 10 papers analyzed".
  - Danh sách Gap Cards: Title, Description, Gap Type (Evaluation Gap / Method Gap / etc.), Confidence %, Evidence count, Suggested Direction.
  - Evidence Papers có thể click xem bài báo gốc.
- **"Supporting patterns" (3 cột):** Top Methods, Datasets, Limitations.
- **Gap Timeline:** Danh sách Gap theo năm, trạng thái Open/Resolved.
- Nút **Regenerate gaps** / **Generate gaps**.

---

### 6.2. Gap Detail Modal

**Bước:** Click **"View gap details"** trên 1 Gap Card.

**Kết quả (Modal):**
- Tiêu đề, mô tả đầy đủ.
- Gap Type, Evidence Count, Confidence %, Level (Low/Medium/High).
- **Suggested direction** (AI gợi ý hướng nghiên cứu).
- **Trend info:** Năm, Status, Papers count, Growth %.
- **Top related papers** (click được).
- **Evidence list:** Trích dẫn từ bài báo gốc.
- **Supporting patterns**.

---

### 6.3. Regenerate Gaps

**Bước:** Click **Regenerate gaps** trên Topic Detail.

**Cơ chế:** Gọi `requestTopicGapGeneration(topicId)` → Backend enqueue Hangfire Job → Frontend poll status mỗi 2.5 giây (tối đa 10 phút).

**Kết quả:**
- Thông báo tiến trình: "Step 1/4: Quality assessment...", "Step 2/4: Extracting paper analyses...", "Step 3/4: Mining patterns...", "Step 4/4: Generating research gaps..."
- Khi xong: "Done — X gaps generated."
- Gap list refresh tự động.

---

### Researcher KHÔNG được:
- Truy cập `/admin/*`
- Quản lý User, Data Source, Sync

---

## 7. Admin

> Đăng nhập `admin@gmail.com` / `Admin123!`. URL Admin: `/admin`

### 7.1. Admin Dashboard

**Bước:** `/admin`

**Kết quả:** Thống kê tổng số User, Paper, Topic, Author. Trạng thái Sync.

---

### 7.2. User Management

**Bước:** `/admin/users`

**Chức năng:**
1. Xem danh sách User, phân trang, tìm kiếm.
2. Filter theo Role, Status.
3. **Kích hoạt/Vô hiệu hóa** tài khoản (PATCH `/api/admin/users/{id}/status`).
4. **Thay đổi Role** (PATCH `/api/admin/users/{id}/role`).

**Lỗi:** Admin tự ban chính mình → Backend từ chối.

---

### 7.3. API & Integrations (Admin API Config Page)

#### 7.3.1. Data Sources

**Bước:** `/admin/api-config` → Section "Data sources"

**Kết quả:** 4 nguồn (SemanticScholar, OpenAlex, Crossref, ArXiv) với toggle bật/tắt và link website.

---

#### 7.3.2. Sync Status

**Kết quả:** Số nguồn đang lock (chạy), Recent Syncs, tìm kiếm status theo từng nguồn.

---

#### 7.3.3. Manual Sync (Trigger thủ công)

**Bước:**
1. Chọn Source, nhập Search Query, Paper Limit.
2. Bấm **Trigger Sync**.

**Kết quả:** Loading spinner → Kết quả (X bài crawl, Y vào Pending) → Pending list refresh.

---

#### 7.3.4. Pending Papers Review

**Bước:**
1. Xem danh sách Pending Jobs.
2. Click 1 Job → Xem chi tiết danh sách bài.
3. Chọn bài (checkbox) → **Approve Selected** / **Reject** Job.
4. Nút **Approve All Pending** → Duyệt tất cả 1 lần.

**Kết quả:** Bài duyệt → status `Approved` → xuất hiện trong search. Bài từ chối → `Rejected`.

---

#### 7.3.5. Sync Schedule

**Bước:** Bật/tắt Schedule, chỉnh Cron Expression, nhập Search Queries → **Save Schedule**.

**Kết quả:** Lịch Hangfire được cập nhật, xuất hiện trong Job History.

---

#### 7.3.6. Sync Logs & Job History

- **Sync Logs:** Lịch sử các lần Sync.
- **Schedule History:** Lịch sử Hangfire Jobs phân trang.

---

### 7.4. Gap Analysis Admin

**Bước:** `/admin/gap-analysis`

**Chức năng:**
1. Danh sách Topics với trạng thái phân tích.
2. Click **Run full pipeline** cho 1 Topic.
3. Hệ thống enqueue Hangfire Job (không block UI).
4. UI hiển thị tiến trình 4 bước theo thời gian thực.

**4 bước Pipeline:**
- **Step 1:** Quality Assessment (đánh giá chất lượng bài báo).
- **Step 2:** Paper Analysis Extraction (AI đọc Abstract/PDF → trích xuất Method, Dataset, Limitation). Tối đa 3 bài/lần chạy, delay 1 giây giữa mỗi bài.
- **Step 3:** Pattern Mining (gom nhóm Method, Dataset, Limitation phổ biến).
- **Step 4:** Research Gap Generation (AI tổng hợp → sinh Research Gaps).

> [!NOTE]
> Backend còn expose endpoint riêng lẻ (Swagger only, không có nút trên UI):
> - Quality: `POST /api/admin/gap-analysis/quality/assess/{topicId}`
> - Extract: `POST /api/admin/gap-analysis/extract/{topicId}`
> - Pattern: `POST /api/admin/gap-analysis/patterns/mine/{topicId}`
> - Gap: `POST /api/admin/gap-analysis/gaps/generate/{topicId}`
> - Regenerate: `POST /api/admin/gap-analysis/gaps/regenerate/{topicId}`
> - Test AI: `GET /api/admin/gap-analysis/test-ai`

---

### 7.5. PDF Management

**Bước:** `/admin/pdf-management`

**Kết quả:** Danh sách bài báo với trạng thái PDF (Pending/Downloading/Ready/Failed). Thống kê tỷ lệ có PDF. Trigger lại download.

---

### 7.6. Hangfire Dashboard

**Truy cập:** `http://localhost:5142/hangfire` (admin / 123456)

**Kết quả:** Monitor tất cả Jobs (Queued, Processing, Succeeded, Failed). Retry job thất bại.

---

## 8. Bảng phân quyền

| Chức năng | Guest | Student | Researcher | Admin |
|-----------|-------|---------|------------|-------|
| Landing Page | ✅ | ✅ | ✅ | ✅ |
| Search Paper / Author | ✅ | ✅ | ✅ | ✅ |
| Paper Detail / Author Detail | ✅ | ✅ | ✅ | ✅ |
| Topics / Topic Detail | ✅ | ✅ | ✅ | ✅ |
| Trends Chart | ✅ | ✅ | ✅ | ✅ |
| Pricing | ✅ | ✅ | ✅ | ✅ |
| Đăng ký / Đăng nhập | ✅ | - | - | - |
| Dashboard | ❌ | ✅ | ✅ | ✅ |
| Bookmark Paper | ❌ | ✅ | ✅ | ✅ |
| Follow Topic/Author/Journal/Paper | ❌ | ✅ | ✅ | ✅ |
| Notifications | ❌ | ✅ | ✅ | ✅ |
| Profile Management | ❌ | ✅ | ✅ | ✅ |
| Upload Avatar | ❌ | ✅ | ✅ | ✅ |
| Đổi mật khẩu | ❌ | ✅ | ✅ | ✅ |
| Upload Documents | ❌ | ✅ | ✅ | ✅ |
| Mua Subscription (PayOS) | ❌ | ✅ | ✅ | ✅ |
| Payment History | ❌ | ✅ | ✅ | ✅ |
| Xem Research Gap của Topic | ❌ | ❌ | ✅ | ✅ |
| Xem Gap Detail / Evidence / Timeline | ❌ | ❌ | ✅ | ✅ |
| Trigger Regenerate Gaps | ❌ | ❌ | ✅ | ✅ |
| Phân tích AI bài báo (PDF) | ❌ | ❌ | ✅ | ✅ |
| Admin Dashboard | ❌ | ❌ | ❌ | ✅ |
| Quản lý User (xem/đổi role/ban) | ❌ | ❌ | ❌ | ✅ |
| Quản lý Data Source | ❌ | ❌ | ❌ | ✅ |
| Trigger Manual Sync | ❌ | ❌ | ❌ | ✅ |
| Duyệt Pending Papers | ❌ | ❌ | ❌ | ✅ |
| Cấu hình Sync Schedule | ❌ | ❌ | ❌ | ✅ |
| Xem Sync Logs / Job History | ❌ | ❌ | ❌ | ✅ |
| Chạy Gap Analysis Pipeline | ❌ | ❌ | ❌ | ✅ |
| Admin PDF Management | ❌ | ❌ | ❌ | ✅ |
| Hangfire Dashboard | ❌ | ❌ | ❌ | ✅ |

---

## 9. Các trường hợp lỗi cần Demo

### Authentication Errors

| Lỗi | Cách demo | Kết quả |
|-----|-----------|---------|
| Login sai mật khẩu | Nhập password sai | Thông báo lỗi Backend |
| Email chưa verify | Đăng ký mới, chưa click link | Thông báo yêu cầu verify |
| Token hết hạn | Để session timeout | Auto refresh token hoặc redirect Login |
| Student vào Admin | Login Student → Vào `/admin` | Redirect `/dashboard` |

### Content Errors

| Lỗi | Cách demo |
|-----|-----------|
| Search không có kết quả | Tìm từ khóa không tồn tại |
| Topic không tồn tại | Vào `/topics/99999` |
| Paper không tồn tại | Vào `/papers/99999` |

### Authorization Errors

| Lỗi | Cách demo |
|-----|-----------|
| Student xem Research Gap | Login Student → Vào Topic Detail → Thấy thông báo Premium |
| Chưa login → Bookmark | Vào Paper Detail chưa login → Click Bookmark → Redirect Login |

### Payment Errors

| Lỗi | Cách demo |
|-----|-----------|
| Cancel thanh toán | Bấm hủy trong PayOS → `/payment/result?status=cancel` |
| Không có gói Active | Xóa gói trong DB → `/pricing` hiển thị "No active plans available" |

### Admin Errors

| Lỗi | Cách demo |
|-----|-----------|
| AI service down | Xóa GroqAI ApiKey → Chạy Pipeline → Lỗi Extraction |
| Sync đang chạy, trigger thêm | Trigger 2 lần liên tiếp → Lần 2 bị lock |
| Admin tự ban mình | Admin tắt account chính mình → Backend từ chối |

---

## 10. Checklist Demo

### Authentication
- ☐ Đăng ký tài khoản mới
- ☐ Verify email (click link)
- ☐ Login thành công
- ☐ Login sai mật khẩu → Xem lỗi
- ☐ Google Login
- ☐ Quên mật khẩu → Nhận email → Reset
- ☐ Logout

### Student Features
- ☐ Xem Dashboard
- ☐ Tìm kiếm Paper với keyword
- ☐ Xem Paper Detail
- ☐ Bookmark 1 bài báo → Xem trang Bookmarks
- ☐ Follow 1 tác giả
- ☐ Follow 1 Topic
- ☐ Xem Topic Detail → Thấy thông báo Premium (không thấy Gap)
- ☐ Xem Trends Chart
- ☐ Xem Notifications
- ☐ Profile → Cập nhật thông tin, Upload Avatar, Đổi mật khẩu
- ☐ Pricing → Click Subscribe Now (demo flow đến PayOS)

### Researcher Features
- ☐ Đăng nhập Researcher
- ☐ Vào Topic Detail → Thấy đầy đủ Research Gaps
- ☐ Click "View gap details" → Xem Gap Detail Modal (Evidence, Trend, Pattern)
- ☐ Click "Regenerate gaps" → Xem tiến trình 4 bước

### Admin Features
- ☐ Login Admin → Xem Admin Dashboard
- ☐ User Management → Tìm user, đổi role lên Researcher
- ☐ User Management → Vô hiệu hóa 1 user
- ☐ API Config → Xem Data Sources, bật/tắt nguồn
- ☐ API Config → Trigger Manual Sync (OpenAlex, "machine learning", 10 bài)
- ☐ API Config → Xem Pending Papers → Duyệt 1 Job
- ☐ API Config → Xem Sync Logs
- ☐ Gap Analysis → Chọn Topic → Run Full Pipeline → Xem 4 bước tiến trình
- ☐ PDF Management → Xem trạng thái PDF
- ☐ Hangfire → `localhost:5142/hangfire` xem Jobs

---

## 11. Kịch bản Demo hoàn chỉnh (MC Script)

---

### Phần 1: Giới thiệu

> *"Xin chào thầy/cô và các bạn. Hôm nay em xin demo hệ thống ScholarTrend — nền tảng phân tích xu hướng nghiên cứu học thuật ứng dụng AI.*
>
> *ScholarTrend giúp sinh viên và nhà nghiên cứu tìm kiếm bài báo khoa học, theo dõi xu hướng nghiên cứu theo thời gian, và quan trọng nhất — xem các khoảng trống nghiên cứu được AI tự động phân tích từ hàng ngàn bài báo thực tế.*
>
> *Hệ thống có 3 role: Student, Researcher và Admin. Em sẽ demo lần lượt."*

---

### Phần 2: Authentication

> *"Đầu tiên, em demo luồng đăng ký và đăng nhập."*

1. Mở `http://localhost:5173/register`.
2. Điền thông tin mẫu → Click **Register**.
3. *"Hệ thống gửi email xác thực. Em đã chuẩn bị sẵn tài khoản verified, nên đăng nhập luôn."*
4. Vào `/login` → Login `thuan@gmail.com` / `Thuan123!`.
5. *"Đã đăng nhập. Hệ thống dùng JWT với refresh token tự động — khi token hết hạn, Frontend tự lấy token mới mà không cần user làm gì."*

---

### Phần 3: Demo Student

> *"Em đang đăng nhập với role Student — role mặc định sau đăng ký."*

**Dashboard:**
> *"Đây là Dashboard cá nhân — hiển thị thống kê và xu hướng mới nhất."*

**Search:**
1. Nhập "artificial intelligence" → Enter.
2. *"Kết quả hiện ngay lập tức vì dữ liệu đã được crawl từ SemanticScholar, OpenAlex, Crossref, ArXiv về local."*
3. Click 1 bài báo.
4. *"Trang Paper Detail: tiêu đề, tác giả, tóm tắt, số citation, DOI, link download PDF. View count tự động tăng."*

**Bookmark:**
1. Click Bookmark.
2. *"Bài đã bookmark. Student có thể vào /bookmarks để đọc sau."*

**Follow:**
1. Click Follow tác giả hoặc Topic.
2. *"Student có thể theo dõi Topic, Author, Journal, Paper để nhận thông báo khi có bài mới."*

**Topic Detail (Student):**
1. Vào Topic "Artificial Intelligence".
2. *"Đây là thống kê Topic: số bài báo, citations, biểu đồ xu hướng theo thời gian."*
3. Scroll xuống phần Research Gaps.
4. *"Phần Research Gap bị khóa với Student. Hệ thống thông báo đây là tính năng Premium — phải nâng cấp lên Researcher."*

**Pricing:**
1. Click **Pricing** trên menu.
2. *"Đây là trang mua gói. Tích hợp cổng thanh toán PayOS — hỗ trợ QR Code và chuyển khoản ngân hàng. Sau khi thanh toán thành công, role tự động nâng lên Researcher qua Webhook."*

---

### Phần 4: Demo Researcher

> *"Bây giờ em đăng nhập bằng tài khoản Researcher — được Admin nâng cấp."*

1. Logout → Login Researcher account.
2. Vào Topic "Artificial Intelligence".
3. *"Với Researcher, phần Research Gap hiện đầy đủ."*
4. *"Hệ thống hiển thị bao nhiêu Gap tìm được, thời gian generate lần cuối, và độ phủ: bao nhiêu trong 10 bài đã được AI đọc."*
5. Mô tả 1 Gap Card:
   - *"Gap này tên 'Evaluation gap for Transfer Learning' — AI phát hiện Transfer Learning dùng nhiều nhưng chưa có benchmark đánh giá cross-dataset đầy đủ. Confidence 46%, loại Evaluation Gap, có 3 bằng chứng từ bài báo thực."*
6. Click **View gap details**.
7. *"Modal Gap Detail: hướng nghiên cứu AI gợi ý, trend info (năm 2026, đang Open), các bài liên quan, và trích dẫn từ phần Conclusion của bài báo làm bằng chứng."*
8. Close modal.
9. Click **Regenerate gaps**.
10. *"Hệ thống đưa Job vào Hangfire — chạy nền, không block UI. Frontend poll status mỗi 2.5 giây và hiển thị tiến trình 4 bước: đánh giá chất lượng, AI đọc bài báo, gom nhóm patterns, sinh Research Gaps. Mỗi lần chạy xử lý tối đa 3 bài mới để tránh timeout."*

---

### Phần 5: Demo Admin

> *"Cuối cùng là phần Admin — quản lý toàn bộ hệ thống."*

1. Logout → Login `admin@gmail.com` / `Admin123!`.
2. Vào `/admin`.

**User Management:**
> *"Admin quản lý người dùng: xem danh sách, tìm kiếm, đổi role, bật/tắt tài khoản."*
1. Vào `/admin/users` → Tìm user.
2. Đổi role lên Researcher → Lưu.
3. *"Tài khoản này giờ có quyền Researcher, có thể xem Research Gap."*

**Data Crawling:**
> *"Đây là phần quản lý luồng dữ liệu — trái tim của hệ thống."*
1. Vào `/admin/api-config`.
2. *"Hệ thống kết nối 4 nguồn dữ liệu học thuật quốc tế. Admin bật/tắt từng nguồn tùy nhu cầu."*
3. Trigger Sync: Chọn OpenAlex, query "machine learning", limit 20 → Click **Trigger Sync**.
4. *"Đang crawl. UI tự polling trạng thái mỗi 2 giây để hiển thị tiến trình."*
5. Sau khi xong → Click Pending Job.
6. *"Bài báo mới kéo về vào hàng đợi Pending Review. Admin kiểm tra trước khi publish."*
7. Chọn bài → Click **Approve Selected**.
8. *"Bài được duyệt ngay vào database, người dùng tìm kiếm được ngay."*

**Gap Analysis Pipeline:**
> *"Admin chạy Pipeline AI cho bất kỳ Topic nào."*
1. Vào `/admin/gap-analysis`.
2. Chọn Topic → **Run full pipeline**.
3. *"Hangfire nhận Job và chạy 4 bước: đánh giá chất lượng, AI đọc bài và trích xuất Method/Dataset/Limitation, gom nhóm patterns, cuối cùng AI tổng hợp sinh Research Gaps. Researcher vào xem ngay sau khi xong."*

---

### Phần 6: Kết thúc

> *"Em vừa demo xong ScholarTrend.*
>
> *Điểm nổi bật:*
> 1. *Thu thập tự động từ 4 nguồn học thuật quốc tế.*
> 2. *AI Pipeline phân tích Research Gap hoàn toàn tự động — đọc thực tế nội dung bài báo, không phải tóm tắt chung chung.*
> 3. *Phân quyền rõ ràng 3 role, JWT với auto refresh token.*
> 4. *Tích hợp thanh toán PayOS và Background Job với Hangfire.*
>
> *Stack kỹ thuật: .NET 9 + PostgreSQL + React + Vite + Hangfire + Groq AI + Gemini AI + PayOS.*
>
> *Cảm ơn thầy/cô! Em sẵn sàng trả lời câu hỏi."*

---

> **Ghi chú kỹ thuật nhanh:**
> - Backend: `.NET 9`, Clean Architecture (Domain / Application / Infrastructure / API)
> - Frontend: `React 18 + Vite`, React Router v6, Recharts
> - Database: `PostgreSQL` + Entity Framework Core
> - Background Jobs: `Hangfire`
> - AI: `Groq AI` (primary) + `Gemini AI` (fallback PDF)
> - Storage: `Backblaze B2` (PDF files, Avatars)
> - Payment: `PayOS` (QR / Bank Transfer)
> - External APIs: SemanticScholar, OpenAlex, Crossref, ArXiv
