# CHTL Manager – Quản lý bán hàng cửa hàng tiện lợi

Phần mềm desktop (WPF, .NET 8) cho một quầy thu ngân của cửa hàng tiện lợi: quét mã vạch tính tiền, tự trừ tồn kho, lưu hóa đơn và xem doanh thu.

## Chức năng

| Vai trò | Chức năng |
|---|---|
| Thu ngân | Bán hàng: quét mã vạch hoặc tìm theo tên (không cần dấu), giỏ hàng, tính tiền thừa, thanh toán |
| Admin | Mọi chức năng của thu ngân, cộng thêm: quản lý sản phẩm (thêm/sửa/xóa, nhập kho), xem lịch sử hóa đơn và thống kê doanh thu |

Quy tắc chính: không bán vượt tồn kho; giá luôn lấy từ CSDL khi thanh toán; thanh toán, trừ kho và lưu hóa đơn nằm trong một giao dịch; sản phẩm đã có trong hóa đơn chỉ bị ngừng kinh doanh chứ không xóa hẳn.

## Kiến trúc

Solution `CHTLManager.slnx` gồm 3 tầng:

```
CHTLManager.WPF  (giao diện)
   ↓
CHTLManager.BLL  (nghiệp vụ: ProductService, OrderService, UserService)
   ↓
CHTLManager.DAL  (EF Core 8 + SQLite)
   ↓
%LocalAppData%\CHTLManager\chtlmanager.db
```

## Yêu cầu hệ thống

- Windows 10/11 64-bit.
- Để chạy: .NET 8 Desktop Runtime (hoặc mới hơn).
- Để build: .NET SDK 8 trở lên, hoặc Visual Studio 2022 (17.13+) với workload ".NET desktop development". Visual Studio không bắt buộc.

## Chạy từ mã nguồn

```bash
dotnet build CHTLManager.slnx
dotnet run --project CHTLManager.WPF
```

Hoặc mở `CHTLManager.slnx` trong Visual Studio, đặt `CHTLManager.WPF` làm Startup Project và nhấn F5.

Tài khoản mẫu:

| Tên đăng nhập | Mật khẩu | Vai trò |
|---|---|---|
| `admin` | `admin123` | Admin |
| `thungan` | `123456` | Thu ngân |

## Cơ sở dữ liệu

- Lần chạy đầu, ứng dụng tự tạo file `%LocalAppData%\CHTLManager\chtlmanager.db`, nạp 2 tài khoản, 12 sản phẩm và khoảng 20 hóa đơn mẫu trong 7 ngày gần nhất.
- CSDL nằm trong thư mục của tài khoản Windows đang dùng: mỗi máy, mỗi tài khoản Windows có một CSDL riêng.
- **Sao lưu:** tắt ứng dụng, copy file `chtlmanager.db` ra nơi khác. **Khôi phục:** chép file đó trở lại đúng vị trí.
- **Làm mới dữ liệu demo:** tắt ứng dụng, xóa file `chtlmanager.db`, mở lại ứng dụng.

## Phạm vi và hạn chế

- Chạy trên **một máy**. SQLite là CSDL dạng file, phù hợp với một quầy, không cần cài máy chủ CSDL. Muốn nhiều máy dùng chung dữ liệu cần đổi sang SQL Server trong mạng LAN hoặc thêm Web API (chỉ thay tầng DAL nhờ kiến trúc phân tầng).
- Phân quyền thực hiện ở giao diện; CSDL không mã hóa.
- Chưa có: in hóa đơn, phiếu nhập kho có lịch sử, trả hàng, khuyến mãi.

Kịch bản demo và câu hỏi bảo vệ: xem [docs/DEMO.md](docs/DEMO.md).
