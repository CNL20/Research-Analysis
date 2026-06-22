# Tài liệu Chức năng: Authentication & Users

Module **Auth** chịu trách nhiệm xử lý toàn bộ vòng đời của người dùng trong hệ thống ScholarTrend, bao gồm xác thực bằng JWT, xác minh danh tính và quản lý hồ sơ.

## 1. Các API Xác thực cơ bản

- 🔓 `POST /api/Auth/register`: Đăng ký tài khoản mới. Trả về thông tin người dùng và tự động gửi **Email xác thực**. (Role mặc định là `LecturerStudent`).
- 🔓 `POST /api/Auth/login`: Đăng nhập bằng Email và Password. Trả về cặp `AccessToken` và `RefreshToken`.
- 🔓 `POST /api/Auth/google-login`: Đăng nhập qua tài khoản Google bằng `id_token`. Trả về JWT Token.
- 🔓 `POST /api/Auth/refresh-token`: Làm mới phiên đăng nhập (nhận Token mới) mà không cần nhập lại Password, yêu cầu truyền vào `RefreshToken` cũ còn hạn.

## 2. Các API Quản lý Mật khẩu & Email

- 🔓 `POST /api/Auth/verify-email`: Gửi token xác minh đã nhận trong email để kích hoạt tài khoản.
- 🔓 `POST /api/Auth/resend-verification`: Gửi lại email chứa link xác minh trong trường hợp chưa nhận được.
- 🔓 `POST /api/Auth/forgot-password`: Yêu cầu cấp lại mật khẩu. Hệ thống gửi email kèm Token bí mật.
- 🔓 `POST /api/Auth/reset-password`: Thay đổi mật khẩu khi bị quên bằng cách xác thực Token từ email.
- 🔒 `POST /api/Auth/change-password`: (Yêu cầu đăng nhập) Đổi mật khẩu dựa trên mật khẩu cũ và mật khẩu mới.

## 3. Các API Quản lý Hồ sơ (Profile)

- 🔒 `GET /api/Auth/profile`: Trả về thông tin chi tiết của người dùng hiện tại (Lấy dựa vào Access Token trong header).
- 🔒 `PUT /api/Auth/profile`: Cập nhật thông tin người dùng (FullName, Institution, ResearchField...).

## 4. Đặc tả cơ chế JWT Token

1. **Access Token (Thời hạn ngắn - VD: 60 phút)**
   - Được gắn vào Header HTTP cho mọi request bảo mật.
   - Format: `Authorization: Bearer <Access_Token>`
   
2. **Refresh Token (Thời hạn dài - VD: 7-30 ngày)**
   - Chỉ dùng để đổi lấy Access Token mới tại `/api/Auth/refresh-token`.
   - Nếu Access Token hết hạn, Frontend (VD: qua Axios Interceptors) sẽ ngầm bắt mã lỗi `401 Unauthorized` và gọi API Refresh Token để làm mới. Nếu Refresh Token cũng hết hạn, người dùng sẽ bị yêu cầu đăng nhập lại.
