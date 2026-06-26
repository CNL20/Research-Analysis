# Hướng Dẫn Chi Tiết Codebase ScholarTrend (Từng Bước Cho Beginner)

Tài liệu này được viết theo phong cách **Mentor hướng dẫn Junior**, giúp bạn hiểu rõ từng dòng code đang chạy như thế nào trong dự án ScholarTrend, dữ liệu đi từ đâu đến đâu, và tại sao kiến trúc lại được thiết kế như vậy.

---

## KIẾN TRÚC TỔNG QUAN CỦA SCHOLARTREND (CLEAN ARCHITECTURE)

Trước khi đi vào từng module, bạn cần hiểu tại sao chúng ta lại chia project thành nhiều file thay vì viết tất cả vào 1 file. Hệ thống sử dụng kiến trúc **3 Tầng (3-Tier) / Clean Architecture** cơ bản:

```text
Client (Frontend React)
   ↓
Controller (API Layer)      -> Lễ tân: Nhận HTTP Request, kiểm tra tính hợp lệ sơ bộ, gọi Service.
   ↓
Service (Business Logic)    -> Quản lý: Chứa não bộ của ứng dụng (Luật kinh doanh, kiểm tra đúng sai).
   ↓
Repository (Data Access)    -> Thủ kho: Chuyên làm việc với Database (chỉ thực hiện SELECT, INSERT, UPDATE, DELETE).
   ↓
DbContext (EF Core)         -> Phiên dịch viên: Dịch code C# sang câu lệnh SQL.
   ↓
Database (SQL Server)       -> Kho lưu trữ vật lý.
```

Nhìn vào source code, bạn sẽ thấy nó được chia thành 4 Project con tương ứng với 4 tầng kiến trúc (Clean Architecture):
* **`ScholarTrend.Domain`**: Đây là tầng cốt lõi nhất (nhân của quả bóng). Nó KHÔNG phụ thuộc vào bất cứ ai. Nó chỉ chứa các `Entity` (như `User`, `ResearchPaper`) và `Enum` (như `UserRole`). Đây là định nghĩa hình dáng các bảng trong DB.
* **`ScholarTrend.Application`**: Tầng này bọc ngoài Domain. Nó chứa `Service`, `DTO`, và `Interfaces`. Đây là nơi chứa luật kinh doanh (ví dụ: đăng nhập sai 5 lần thì khoá tài khoản).
* **`ScholarTrend.Infrastructure`**: Tầng này chứa công nghệ thực tế. Nó đi làm "culi" cho Application. Nó chứa `DbContext`, `Repository`, và code gọi API bên ngoài (Semantic Scholar).
* **`ScholarTrend.API`**: Tầng ngoài cùng, bọc lấy tất cả. Nó chứa `Controller` để mở cổng giao tiếp ra Internet cho Frontend gọi vào.

### Tại sao lại chia tầng như vậy?
1. **Vì sao dùng Controller?** Để tách biệt phần giao tiếp mạng (HTTP, Routing, JSON) ra khỏi logic. Nếu sau này bạn không làm Web API nữa mà làm app Desktop, bạn chỉ cần vứt Controller đi, giữ nguyên Service.
2. **Vì sao dùng Service?** Để xử lý logic phức tạp. Nếu không có Service, bạn viết mọi thứ vào Controller thì file Controller sẽ dài hàng ngàn dòng, cực kỳ khó bảo trì và không thể test tự động (Unit Test).
3. **Vì sao cần Repository?** Repository giúp gom các câu lệnh query database lại một chỗ. Service không cần quan tâm đến Entity Framework hoạt động ra sao, nó chỉ cần gọi `repository.GetById(1)` là xong.
4. **Vì sao cần DTO (Data Transfer Object)?** DTO là cái "Thùng hàng" để chuyển dữ liệu. Ví dụ Entity `User` trong DB có cột `PasswordHash`, ta tuyệt đối không được trả `User` về Frontend để tránh lộ mật khẩu. Ta tạo một `UserProfileDto` chỉ chứa `Tên` và `Email` rồi gửi về FE.
5. **Vì sao cần Dependency Injection (DI)?** Để các tầng gọi nhau mà không bị phụ thuộc cứng (không dùng `new Service()`). DI giúp tiêm (inject) các file vào nhau tự động lúc chương trình chạy, rất linh hoạt.

---

## 🟢 MODULE 1: AUTHENTICATION (PHASE 1)

**Chức năng này dùng để làm gì?** Quản lý việc người dùng đăng nhập vào hệ thống và cấp cho họ một "thẻ ra vào" (JWT Token) để truy cập các tính năng bảo mật.

**User thao tác trên Frontend:** 
Người dùng nhập Email và Password vào form Login trên React. Bấm nút "Đăng nhập". Frontend sẽ gửi một HTTP POST request lên Backend.

### Luồng thực thi (Flow):
```text
Client (ReactJS)
   ↓ Gửi POST /api/auth/login kèm Body { email, password }
AuthController.cs (Lễ tân)
   ↓ Gọi _authService.LoginAsync(request)
IAuthService.cs (Hợp đồng/Interface)
   ↓ Chuyển đến file implement thực tế
AuthService.cs (Quản lý - Xử lý logic)
   ↓ Gọi UserManager (Đóng vai trò như Repository của Identity)
UserManager (Của ASP.NET Core Identity)
   ↓ 
ScholarTrendDbContext.cs
   ↓ Thực thi câu lệnh SELECT ... FROM AspNetUsers
SQL Server Database
```

### Giải thích chi tiết dòng code chạy qua từng file:

**1. `AuthController.cs` (Nhận Request)**
```csharp
[HttpPost("login")]
public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
{
    // [FromBody] LoginRequest: Lễ tân nhận thùng hàng từ FE. Thùng hàng này chứa Email, Password.
    // Nếu FE gửi thiếu data, tính năng Validator (cài ở Program.cs) sẽ tự động chặn lại trước cả khi vào hàm này.
    
    // Gọi Service xử lý logic
    var response = await _authService.LoginAsync(request);
    
    // Đóng gói kết quả thành JSON trả về Frontend
    return Ok(ApiResponse<AuthResponse>.Success(response, "Login successful."));
}
```

**2. `AuthService.cs` (Não bộ xử lý)**
```csharp
public async Task<AuthResponse> LoginAsync(LoginRequest request)
{
    // (READ) Truy vấn Database: Tìm user có email này trong bảng AspNetUsers.
    var user = await _userManager.FindByEmailAsync(request.Email);
    
    // Kiểm tra logic 1: User không tồn tại
    if (user == null) {
        throw new InvalidOperationException("Invalid email or password.");
    }

    // Kiểm tra logic 2: Password sai
    // Dòng này gọi Database để lấy mật khẩu đã mã hóa (Hash) ra so sánh với mật khẩu user nhập.
    var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
    if (!isPasswordValid) {
        throw new InvalidOperationException("Invalid email or password.");
    }

    // (UPDATE) Cập nhật dữ liệu: Cập nhật thời gian đăng nhập cuối cùng vào bảng
    user.LastLoginAt = DateTime.UtcNow;
    await _userManager.UpdateAsync(user);

    // Sinh Token (JWT) - Dòng này tạo ra chuỗi mã hóa ký bằng SecretKey.
    return await BuildAuthResponseAsync(user);
}
```

**Ví dụ Request thực tế:**
* **Request:** `POST /api/auth/login`
  ```json
  { "email": "admin@gmail.com", "password": "Admin123!" }
  ```
* **Database thay đổi:** Bảng `AspNetUsers` được đọc, sau đó cột `LastLoginAt` được cập nhật.
* **Response:**
  ```json
  {
      "success": true,
      "message": "Login successful.",
      "data": {
          "token": "eyJhbGciOiJIUzI1NiIsInR...", 
          "refreshToken": "abcdef123456...",
          "email": "admin@gmail.com",
          "fullName": "Admin",
          "roles": ["Admin"]
      }
  }
  ```

**Tóm tắt đơn giản:** 
Khi user bấm nút Đăng nhập, Frontend gửi email/mật khẩu lên `AuthController`. Controller ném cục data qua cho `AuthService`. Service nhờ `UserManager` chạy xuống DB lấy thông tin user lên kiểm tra mật khẩu. Nếu đúng, Service update ngày giờ đăng nhập, chế tạo một cái thẻ (JWT Token) gửi lại cho Controller. Controller bọc thẻ đó vào hộp JSON gửi về Frontend.

### 🔑 Kiến thức bổ sung quan trọng trong Auth:

**1. Hash Password là gì? (Mã hoá một chiều)**
Hệ thống KHÔNG BAO GIỜ lưu mật khẩu của user (ví dụ: `123456`) dưới dạng chữ rõ ràng vào Database. Mật khẩu sẽ bị băm (Hash) thành một chuỗi ngoằn ngoèo kiểu `AQAAAAEAACcQAAAAEG...`. 
- Đặc điểm của Hash là **chỉ có thể băm chiều đi, không thể giải mã chiều ngược lại**. Kể cả Admin hay Hacker truy cập được Database cũng không thể biết mật khẩu gốc của User là gì.
- Vậy làm sao để kiểm tra lúc đăng nhập? Khi user gõ `123456`, hệ thống sẽ băm cái chữ `123456` đó ra. Nếu chuỗi băm mới giống y hệt chuỗi băm trong DB -> Mật khẩu đúng (`CheckPasswordAsync` làm việc này).

**2. Access Token là gì?**
Sau khi đăng nhập thành công, thay vì mỗi lần lấy dữ liệu đều phải gửi lại username/password, hệ thống cấp cho bạn một cái "Thẻ ra vào" (Access Token). Thẻ này là chuẩn JWT (JSON Web Token), bên trong nó có ghi sẵn "Tên tôi là A, chức vụ Admin, thẻ hết hạn vào 12h trưa".
- Bất cứ khi nào Frontend muốn lấy dữ liệu bảo mật, nó chỉ cần giơ cái thẻ này ra (đính kèm vào Header của Request).
- **Nhược điểm:** Thẻ này rất dễ bị ăn trộm (nếu ai đó dùng máy tính của bạn). Nên để an toàn, thẻ Access Token thường có **tuổi thọ rất ngắn** (vd: 60 phút).

**3. Refresh Token là gì?**
Vì Access Token chỉ sống được 60 phút, nếu bạn đang lướt web mà thẻ hết hạn, hệ thống sẽ bắt bạn nhập lại Password. Rất phiền phức!
- Do đó, lúc đăng nhập, hệ thống cấp thêm 1 thẻ nữa gọi là **Refresh Token** (tuổi thọ dài: 7 ngày - 30 ngày).
- Thẻ này không dùng để lấy dữ liệu. Nó CẤT GIẤU rất kỹ trên Frontend.
- Khi Access Token hết hạn (bị báo lỗi 401), Frontend sẽ tự động, âm thầm gửi cái Refresh Token lên Backend qua API `/refresh-token` để xin một cái Access Token Mới cứng 60 phút nữa. Người dùng sẽ không hề hay biết và không phải gõ lại mật khẩu!

---

## 🟡 MODULE 2: CORE FEATURES (PHASE 2)

### 1. TÍNH NĂNG SEARCH PAPER (TÌM KIẾM BÀI BÁO)

**Dùng để làm gì?** Cho phép user gõ từ khóa để tìm các bài báo trong hệ thống.
**User thao tác:** Gõ "Machine Learning" vào thanh search trên Frontend, nhấn Enter.

**Luồng thực thi:**
```text
Client -> PapersController -> IPaperService -> PaperService -> IResearchPaperRepository -> ResearchPaperRepository -> DbContext -> DB (Bảng ResearchPapers, Authors, Journals)
```

**Code thực thi (Tại `ResearchPaperRepository.cs`):**
```csharp
// Repository chuyên làm việc với DB
public async Task<PagedResult<ResearchPaper>> SearchAsync(PaperQueryParameters query)
{
    // Khởi tạo câu query gốc: Lấy danh sách bài báo
    var queryable = _context.ResearchPapers
        .Include(p => p.Journal)          // JOIN bảng Journals
        .Include(p => p.PaperAuthors)     // JOIN bảng phụ PaperAuthors
        .ThenInclude(pa => pa.Author)     // JOIN tiếp bảng Authors
        .AsQueryable();

    // Dòng này kiểm tra: Nếu FE có truyền từ khóa Search
    if (!string.IsNullOrWhiteSpace(query.SearchTerm))
    {
        // (READ) Lọc dữ liệu: Câu lệnh này sẽ được dịch thành SQL LIKE '%TừKhóa%'
        queryable = queryable.Where(p => p.Title.Contains(query.SearchTerm) || 
                                         p.Abstract.Contains(query.SearchTerm));
    }

    // Skip và Take dùng để Phân trang (Pagination)
    var items = await queryable
        .Skip((query.PageNumber - 1) * query.PageSize)
        .Take(query.PageSize)
        .ToListAsync();

    return new PagedResult<ResearchPaper>(items, totalCount, query.PageNumber, query.PageSize);
}
```
**Tại sao cần `.Include`?** Trong Database SQL, bài báo và Tác giả nằm ở 2 bảng khác nhau, nối nhau bằng bảng trung gian `PaperAuthors`. Lệnh `Include` chính là lệnh `JOIN` trong SQL. Nếu bỏ dòng này, khi trả dữ liệu về, danh sách Tác giả sẽ bị rỗng (`null`).

---

### 2. TÍNH NĂNG PAPER DETAIL (XEM CHI TIẾT)

**Dùng để làm gì?** User click vào 1 bài báo để xem toàn bộ nội dung, abstract, năm xuất bản.

**Luồng thực thi:**
```text
Client -> PapersController (GetById) -> PaperService -> ResearchPaperRepository -> DB
```

**Code thực thi (Tại `PaperService.cs`):**
```csharp
public async Task<PaperDetailDto> GetByIdAsync(int id)
{
    // 1. Service nhờ Repository lấy paper từ DB
    var paper = await _paperRepository.GetByIdWithDetailsAsync(id);
    
    if (paper == null) {
        throw new NotFoundException("Paper not found"); // Báo lỗi 404 nếu ID không tồn tại
    }

    // 2. Mapping: Dòng này dùng AutoMapper (hoặc viết tay) để biến Entity 'paper' (mang nhiều dữ liệu rác, nhạy cảm) thành 'PaperDetailDto' (chỉ chứa data cần thiết cho FE).
    var dto = _mapper.Map<PaperDetailDto>(paper);

    return dto; // Trả về Controller
}
```

---

### 3. TÍNH NĂNG BOOKMARK (LƯU BÀI BÁO)

**Dùng để làm gì?** User bấm biểu tượng Bookmark (Lưu) để đọc lại sau.
**User thao tác:** Bấm icon Bookmark trên Frontend. Frontend lấy Token gửi vào Header để chứng minh user đã đăng nhập, gọi API POST `/api/bookmarks`.

**Luồng thực thi:**
```text
Client (Gửi kèm JWT Token) -> BookmarksController -> BookmarkService -> BookmarkRepository -> DbContext -> Bảng Bookmarks (INSERT)
```

**Code thực thi (Tại `BookmarksController.cs`):**
```csharp
[Authorize] // Attribute này kiểm tra JWT. Nếu FE không gửi Token hoặc Token hết hạn, lập tức bị đá ra (trả về 401 Unauthorized) mà chưa kịp chạy vào hàm.
[HttpPost]
public async Task<ActionResult> AddBookmark([FromBody] CreateBookmarkRequest request)
{
    // Lấy ID của người dùng từ Token. Backend tự động phân tích JWT Header để lấy ra userId. Rất an toàn, FE không thể fake được ID.
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    
    // Chuyển cho Service lưu
    await _bookmarkService.AddBookmarkAsync(userId, request.PaperId);
    return Ok();
}
```

**Code thực thi (Tại `BookmarkService.cs`):**
```csharp
public async Task AddBookmarkAsync(string userId, int paperId)
{
    // 1. Kiểm tra Bài báo có tồn tại trong DB không? (Tránh FE gửi bậy ID = 999999)
    var paperExists = await _paperRepository.ExistsAsync(paperId);
    if (!paperExists) throw new NotFoundException();

    // 2. Kiểm tra xem user đã bookmark bài này chưa? (Tránh lưu trùng)
    var existing = await _bookmarkRepository.FindUserBookmarkAsync(userId, paperId);
    if (existing != null) return; // Nếu đã lưu rồi thì bỏ qua không làm gì cả

    // 3. (CREATE) Thêm dữ liệu mới vào bảng
    var bookmark = new Bookmark {
        UserId = userId,
        PaperId = paperId,
        CreatedAt = DateTime.UtcNow
    };

    // Repository chỉ đưa dữ liệu vào bộ nhớ tạm (Tracking) của DbContext
    await _bookmarkRepository.AddAsync(bookmark);
    
    // Lệnh này mới THỰC SỰ chạy câu SQL INSERT INTO Bookmarks... xuống SQL Server.
    await _unitOfWork.SaveChangesAsync(); 
}
```
**Tại sao cần `_unitOfWork.SaveChangesAsync()`?**
Pattern `UnitOfWork` gom nhiều thao tác vào chung một giao dịch (Transaction). Giả sử bạn lưu Bookmark và đồng thời phải cập nhật số lượng `TotalBookmarks` trong bảng User. Nếu lưu Bookmark thành công nhưng cập nhật User bị lỗi sập server, `UnitOfWork` sẽ tự động Undo (Rollback) xóa cái Bookmark vừa lưu, đảm bảo dữ liệu không bị mâu thuẫn.

**Tóm tắt luồng Bookmark:**
Khi user bấm nút Save, hệ thống kiểm tra "thẻ ra vào" (JWT) của user xem có hợp lệ không. Nếu hợp lệ, hệ thống bóc thẻ ra lấy ID của user đó. Tiếp theo, hệ thống rà soát xem bài báo đó có thực sự tồn tại và user đã lưu nó chưa. Nếu mọi thứ hợp lệ, nó nhét một bản ghi mới chứa (UserId, PaperId) vào bảng `Bookmarks` trong Database và lưu lại. Tới đây quy trình kết thúc!
