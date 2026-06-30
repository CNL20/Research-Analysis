# Hướng dẫn Cài đặt & Chạy Dự án

Tài liệu này hướng dẫn cách cấu hình môi trường, chạy ứng dụng Backend và truy cập các công cụ hỗ trợ.

## 1. Yêu cầu hệ thống
- **.NET 9 SDK** cài đặt sẵn trên máy.
- **PostgreSQL** 14+ (local Docker, Supabase, Neon, hoặc Railway).
- (Khuyên dùng) Trình soạn thảo Visual Studio 2022 hoặc VS Code.

### Chạy PostgreSQL local bằng Docker (khuyên dùng)

```bash
docker compose up -d
```

## 2. Cấu hình Môi trường

Copy `ScholarTrend.API/appsettings.example.json` thành `appsettings.Development.json` (file này không commit lên git). Cấu hình các mục quan trọng sau:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=ScholarTrendDb;Username=postgres;Password=postgres"
  },
  "Authentication": {
    "Jwt": {
      "SecretKey": "YourSuperSecretKeyAtLeast32CharactersLong!"
    }
  },
  "EmailSettings": {
    "SenderName": "KIDIO",
    "SenderEmail": "kidioteamBOF@gmail.com",
    "ApiKey": "<BREVO_API_KEY_CUA_BAN>"
  }
}
```
*Lưu ý: Bạn bắt buộc phải khai báo `SecretKey` (trên 32 ký tự) thì API Auth mới hoạt động được.*

## 3. Khởi động ứng dụng

Mở terminal tại thư mục gốc của Solution và chạy lệnh:

```bash
dotnet build
dotnet run --project ScholarTrend.API
```

Ứng dụng sẽ chạy ở môi trường Development. Bạn có thể truy cập các đường dẫn sau (Port mặc định có thể là `5141` hoặc theo `launchSettings.json`):

- **Swagger UI (Danh sách API)**: `http://localhost:5141/swagger` hoặc `https://localhost:7085/swagger`
- **Hangfire Dashboard (Quản lý Job ngầm)**: `http://localhost:5141/hangfire` hoặc `https://localhost:7085/hangfire`

## 4. Dữ liệu mẫu (Seeder)

Khi hệ thống khởi chạy lần đầu tiên và phát hiện Database trống, tính năng tự động tạo dữ liệu (`DatabaseSeeder.cs`) sẽ tự động được kích hoạt, bao gồm:
- Tạo Roles mặc định.
- Tạo **5 tài khoản Test** (xem bên dưới).
- Khởi tạo dữ liệu cho: Keywords, Authors, Journals, Topics.
- Sinh ra **20 bài báo khoa học** (Research Papers) giả lập kèm trích dẫn (Citations).
- Khởi tạo cấu hình cho External APIs (SemanticScholar, OpenAlex).

### Tài khoản Test có sẵn:
- **Admin**: `admin@gmail.com` | Pass: `Admin123!`
- **User (LecturerStudent)**: `thuan@gmail.com`, `tien@gmail.com`, `lan@gmail.com`, `nam@gmail.com` | Pass format: `[Name]123!` (VD: `Thuan123!`)

## 5. Xác thực (Authorization) trên Swagger

Để gọi các API có khóa (`Authorize`), bạn làm theo các bước:
1. Mở endpoint `POST /api/Auth/login` trên Swagger.
2. Nhập email & password (ví dụ admin). Bấm Execute.
3. Trong Response trả về, copy dòng mã token ở mục `token`.
4. Kéo lên đầu trang Swagger, bấm nút **Authorize** màu xanh lá.
5. Nhập vào ô trống chữ: `Bearer <token vừa copy>`. Bấm Save.
6. Từ giờ mọi API của bạn đều đã được xác thực!
