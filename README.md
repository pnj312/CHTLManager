# CHTL Manager – Quản lý bán hàng cửa hàng tiện lợi

Phần mềm desktop (WPF, .NET 8) cho một quầy thu ngân của cửa hàng tiện lợi: quét mã vạch tính tiền, tự trừ tồn kho, lưu hóa đơn và xem doanh thu.

## Chức năng

| Vai trò | Chức năng |
|---|---|
| Thu ngân | Bán hàng: quét mã vạch hoặc tìm theo tên (không cần dấu), giỏ hàng, tính tiền thừa, thanh toán |
| Admin | Mọi chức năng của thu ngân, cộng thêm: quản lý sản phẩm (thêm/sửa/xóa, nhập kho, kiểm kê), xem lịch sử hóa đơn và thống kê doanh thu |

Quy tắc chính:
- Bán hàng trừ tồn kho đúng số lượng bán; thanh toán, trừ kho và lưu hóa đơn nằm trong một giao dịch. Giá luôn lấy từ CSDL khi thanh toán.
- **Kho báo không đủ nhưng hàng thực tế vẫn còn** (hàng nhập chưa ghi, đếm sai...): thu ngân xác nhận "vẫn bán" thì hóa đơn vẫn lưu đầy đủ, tồn kho được phép âm. Sản phẩm tồn âm hiện màu đỏ; Admin dùng **Kiểm kê** để đặt tồn kho bằng số đếm thực tế (cũng dùng khi hàng mất, hỏng).
- Sản phẩm đã có trong hóa đơn chỉ bị ngừng kinh doanh chứ không xóa hẳn; thêm lại cùng mã vạch thì kinh doanh lại.

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
- Để chạy bản đóng gói (`.exe`): không cần cài gì thêm. Để chạy từ mã nguồn: .NET 8 Desktop Runtime (hoặc mới hơn).
- Để build: .NET SDK 8 trở lên, hoặc Visual Studio 2022 (17.13+) với workload ".NET desktop development". Visual Studio không bắt buộc.

## Chạy từ mã nguồn

```bash
dotnet build CHTLManager.slnx
dotnet run --project CHTLManager.WPF
```

Hoặc mở `CHTLManager.slnx` trong Visual Studio, đặt `CHTLManager.WPF` làm Startup Project và nhấn F5.

## Đóng gói để chạy trên máy khác (không cần cài .NET)

```bash
dotnet publish CHTLManager.WPF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

- Kết quả: một file duy nhất `publish\CHTLManager.WPF.exe` (khoảng 71 MB, đã gồm .NET runtime và SQLite).
- Cài trên máy mới: copy file `.exe` sang (USB, Google Drive...) và nhấp đúp để chạy. Không cần cài .NET hay Visual Studio. Windows SmartScreen có thể cảnh báo vì file chưa ký số: chọn "More info" → "Run anyway".
- Lần mở đầu mất vài giây để giải nén và tạo CSDL; các lần sau nhanh hơn.
- Cập nhật phiên bản: thay file `.exe` mới, CSDL trong `%LocalAppData%` được giữ nguyên.

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
- Chưa có: in hóa đơn, phiếu nhập kho và lịch sử kiểm kê, trả hàng, khuyến mãi.

Kịch bản demo và câu hỏi bảo vệ: xem [docs/DEMO.md](docs/DEMO.md).
