# CHTL Manager – Quản lý bán hàng cửa hàng tiện lợi

Solution `CHTLManager.slnx`: DAL (EF Core + SQLite) → BLL (3 service) → WPF (.NET 8).

## Chạy
1. Cài Visual Studio 2022 (hỗ trợ .slnx) + workload ".NET desktop development" (hoặc .NET SDK 8+ trên Windows).
2. Mở `CHTLManager.slnx`, đặt `CHTLManager.WPF` làm Startup Project, nhấn F5.
3. CSDL tự tạo tại `%LocalAppData%\CHTLManager\chtlmanager.db`.
4. Tài khoản mẫu: `admin / admin123` (Admin), `thungan / 123456` (Thu ngân).

> Lưu ý: mã nguồn được viết nhưng chưa biên dịch trong môi trường tạo bài (không có .NET SDK). Nếu Visual Studio báo lỗi, gửi lại nội dung lỗi để sửa.
