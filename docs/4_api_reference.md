# 4. API Reference (Tài liệu API)

Tài liệu này đặc tả quy ước thiết kế API và danh sách API của các module.

---

## 1. Định dạng phản hồi chung (Response Wrapper)

Hệ thống cung cấp một lớp Wrapper chuẩn cho **mọi API** có tên `ApiResponse<T>`, giúp bên FrontEnd dễ dàng tích hợp mà không phải đoán cấu trúc:

**Thành công (200 OK):**
```json
{
  "success": true,
  "message": "Success",
  "data": { "id": 1, "name": "AI" },
  "errors": null
}
```

**Thất bại Nghiệp vụ (400 Bad Request / 404 Not Found):**
```json
{
  "success": false,
  "message": "Topic not found.",
  "data": null,
  "errors": null
}
```

**Lỗi Validation (400 - FluentValidation):**
```json
{
  "success": false,
  "message": "Validation failed.",
  "data": null,
  "errors": ["Email is required.", "Password must be at least 6 characters."]
}
```

---

## 2. Xác thực JWT (Authentication)

1. Gọi API **`POST /api/auth/login`**. Output nhận về bao gồm `token` (chứa claims userId, email, roles) và `refreshToken`.
2. Gửi Request có kèm HttpHeader xác thực lên máy chủ: 
   `Authorization: Bearer <token_vừa_nhận>`
3. Hệ thống cấu hình Token hết hạn (VD 60 phút). Nếu gặp lỗi UnAuthorized (401), Frontend gọi đến `POST /api/auth/refresh-token` gửi refreshToken cũ vào -> Lấy Token mới và tiếp tục gọi lại. 
(Tránh bắt user phải Log In lại nhiều lần).

---

## 3. Danh sách Endpoints quan trọng

**Base URL:** `http://localhost:5141`
**Ký hiệu quyền:**
- 🔓 Public — không cần token
- 🔒 Auth — cần JWT (mọi role)
- 🔬 Researcher — Admin hoặc Researcher
- 👑 Admin — chỉ Admin

*(Lưu ý: Để test dễ hơn, mời tham khảo chi tiết Input Body tại Swagger UI `http://localhost:5141/swagger`)*

### A. Auth & Users
- 🔓 `POST /api/auth/register`: Đăng ký tài khoản mới (LecturerStudent).
- 🔓 `POST /api/auth/login`: Xác thực mật khẩu và sinh JWT.
- 🔓 `POST /api/auth/refresh-token`: Cấp lại JWT.
- 🔒 `GET /api/auth/profile`: Lấy thông tin user hiện tại.
- 👑 `GET /api/admin/users`: Giám sát user (Filter, Status).
- 👑 `PATCH /api/admin/users/{id}/role`: Phân lại Rule.

### B. Search & Core Entity
- 🔒 `GET /api/papers/search`: Tìm kiếm có tham số phân trang, bộ lọc Topic/Journal, Keyword. (Hệ thống Auto Log Search History).
- 🔒 `GET /api/papers/{id}`: Paper Detail.
- 🔒 `POST /api/bookmarks/{paperId}`: Đánh dấu bài báo.
- 🔒 `GET /api/topics/{id}`: Xem Topic Details (Sẽ chứa kèm đồ thị biểu diễn Trend).

### C. Trend Engine
- 🔒 `GET /api/trends/dashboard`: Lấy cục Data lớn Render biểu đồ trang nhất (Có hệ thống memory Caching 1h chống DDoSs Db).
- 🔒 `GET /api/trends/keywords/top`: GET Top Keyword có `TrendingScore` cao nhất hiện thời.
- 🔬 `POST /api/trends/compare`: Phân tích chuyên sâu so sánh. Nhận vào mảng ID của topic/journal để so sánh Data.

### D. Sync Jobs (Đồng bộ)
- 👑 `POST /api/admin/sync/trigger`: Gọi cưỡng ép đồng bộ từ Semantic Scholar.
- 👑 `GET /api/admin/sync/logs`: Log lịch sử Sync.

### E. Analytics
- 🔬 `GET /api/reports/publications`: Truy xuất Data dạng JSON Grouping theo Năm / Keyword.
- 🔬 `GET /api/reports/export/csv`: Force tải file CSV Download stream từ backend.
