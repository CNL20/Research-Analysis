# 🎓 ScholarTrend Project

**Hệ thống theo dõi xu hướng công bố báo chí khoa học** (Scientific Journal Publication Trend Tracking System).

Dự án SWP — nhóm SU26SWP06. Backend REST API xây dựng trên **.NET 9**, kế trúc **Clean Architecture**.

Hỗ trợ cung cấp đầy đủ chức năng từ xác thực người dùng, tìm kiếm bài báo, phân tích luồng dữ liệu Trend, gợi ý cá nhân hóa, đồng bộ data Job Background (Hangfire), đến dashboard và báo cáo.

---

## 📚 Tài liệu dự án (Documentation)

Chào mừng bạn, toàn bộ cẩm nang thiết lập và nghiệp vụ của Backend ScholarTrend đã được chia nhỏ theo từng chuyên môn để dễ tra cứu tại thư mục `docs/`:

1. 🚀 [**1. Getting Started (Setup & Run)**](docs/1_getting_started.md)
   *Cách thiết lập Database, Config JSON, các lệnh build dự án, tài khoản Test, và CURL sample.*
   
2. 🏛️ [**2. Architecture (Kiến trúc hệ thống)**](docs/2_architecture.md)
   *Mô hình Clean Architecture 4 Layer, ý nghĩa cấu trúc thư mục, lý do chọn công nghệ kỹ thuật.*
   
3. 💼 [**3. Business Logic (Nghiệp vụ cốt lõi)**](docs/3_business_logic.md)
   *Luồng chức năng User, Roles, Background Sync Semantic Scholar, Data Flow của Trend Engine.*
   
4. 🔌 [**4. API Reference (Tài liệu API)**](docs/4_api_reference.md)
   *Định cấu trúc Response của System, thiết kế JWT. Danh sách Endpoint của phân hệ Auth, Trend, Sync, Analytics.*

---

## 🛠️ Công nghệ tóm tắt

- **Framework:** .NET 9 ASP.NET Core Web API (RESTful)
- **Database:** Entity Framework Core 9 + SQL Server
- **Authentication:** ASP.NET Identity + JWT Bearer
- **Job Background:** Hangfire (SQL Server storage)
- **Unit Test:** xUnit + Moq + FluentAssertions
- **Data Source:** Semantic Scholar, OpenAlex

---

> 💡 **Mẹo:** Project này cung cấp sẵn Data mẫu (Fake Seeding Database) cho lần chạy đầu tiên. Hãy xem file `docs/1_getting_started.md` để dùng thử nhé!
