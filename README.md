# CHTL Manager – Quản lý bán hàng cửa hàng tiện lợi

Phần mềm desktop (WPF, .NET 8) cho một quầy thu ngân của cửa hàng tiện lợi: quét mã vạch tính tiền, quản lý tồn kho theo phiếu nhập, biết lợi nhuận, công nợ nhà cung cấp và thất thoát.

## Chức năng

| Vai trò | Chức năng |
|---|---|
| Thu ngân | Bán hàng: quét mã vạch hoặc tìm theo tên (không cần dấu), giỏ hàng, tính tiền thừa, thanh toán. Nhận hàng khi nhà cung cấp giao (không thấy giá). Đếm hàng khi nhận. Tự đổi mật khẩu |
| Admin | Bán hàng và đổi mật khẩu như thu ngân (nhận hàng bằng "Nhập hàng mới" có nhập giá, thay cho nút Nhận hàng), cộng thêm: sản phẩm và giá vốn, phiếu nhập, xác nhận và sửa giá phiếu, công nợ nhà cung cấp, kiểm kê và xử lý chênh lệch, thu và sửa khoản bồi thường, báo cáo doanh thu và lãi gộp, hủy hóa đơn, nhật ký kho, tài khoản |

Quy tắc chính:
- Thanh toán, trừ kho và lưu hóa đơn nằm trong một giao dịch. Giá bán và giá vốn luôn đọc lại từ CSDL.
- **Giá vốn** bình quân gia quyền, tính lại sau mỗi phiếu nhập có giá, lưu vào từng dòng hóa đơn lúc bán. Hủy hoặc sửa giá một lô thì giá vốn được tính lại theo các lô còn lại.
- **Phiếu nhập:** một nhà cung cấp + một số hóa đơn, nhiều dòng; công nợ theo số **thực nhận** × giá nhập; không nhập trùng hóa đơn. Thu ngân nhận hàng thì kho tăng ngay, Admin đối chiếu và nhập giá sau; hàng chờ giá chưa vào giá vốn, bán vào phần đó thì dòng hóa đơn được đánh dấu giá vốn tạm tính.
- **Kho báo hết nhưng khách vẫn cầm được hàng:** thu ngân xác nhận thì bán được (tối đa 10 cái vượt tồn mỗi dòng), hóa đơn đầy đủ, sản phẩm hiện `0 (bán ngoài sổ: N)` màu đỏ. Trước khi nhập lô mới phải kiểm kê chốt lô cũ, đếm trước khi xếp hàng mới lên kệ.
- **Kiểm kê hai bước:** đếm làm tồn kho đúng ngay, Admin xử lý phần lệch sau. Thiếu: thất thoát, người chịu trách nhiệm bồi thường, hoặc đếm sai (bắt buộc ghi chú). Thừa: lô chưa ghi (sinh phiếu và công nợ), thu nhập khác, hoặc đếm sai. Sản phẩm chưa có giá vốn thì phải khai báo giá vốn trước khi ghi thất thoát, bồi thường hoặc thu nhập khác.
- **Lãi trong báo cáo** là lãi gộp sau chênh lệch kho, **chưa trừ** lương, mặt bằng, điện nước. Phần mềm chỉ theo dõi số phải thu bồi thường, không tự trừ lương.
- Sửa sai không xóa dữ liệu: hủy kèm lý do (hóa đơn, phiếu nhập, lần trả tiền, lần thu bồi thường); sửa giá phiếu đã xác nhận bằng **Sửa giá nhập**; sửa người hoặc số tiền bồi thường bằng **Sửa khoản bồi thường**. Hai thao tác sửa này được ghi vào nhật ký kho kèm giá trị cũ, giá trị mới và lý do. Nhật ký kho không có chức năng sửa, xóa.
- Sản phẩm còn tồn không xóa được; đã có lịch sử thì chỉ ngừng kinh doanh, thêm lại cùng mã vạch thì kinh doanh lại với tồn kho giữ nguyên.
- Tài khoản: mật khẩu tối thiểu 6 ký tự, lưu dạng băm; không thể tự khóa mình hay khóa Admin cuối cùng.

## Kiến trúc

Solution `CHTLManager.slnx` gồm 3 tầng ứng dụng và project kiểm thử `CHTLManager.Tests`:

```
CHTLManager.WPF  (giao diện)
   ↓
CHTLManager.BLL  (nghiệp vụ: Product, Order, Purchase, StockCount, User)
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

Thêm `-- --demo` để nạp dữ liệu mẫu khi cửa hàng chưa có dữ liệu.

Hoặc mở `CHTLManager.slnx` trong Visual Studio, đặt `CHTLManager.WPF` làm Startup Project và nhấn F5.

## Kiểm thử

```bash
dotnet test CHTLManager.Tests
```

Bộ test (xUnit) kiểm tra tầng nghiệp vụ trên database tạm, không đụng dữ liệu thật. Giao diện được kiểm tra bằng cách chạy thử.

## Đóng gói để chạy trên máy khác (không cần cài .NET)

```bash
dotnet publish CHTLManager.WPF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

- Kết quả: thư mục `publish` gồm `CHTLManager.WPF.exe` (khoảng 71 MB, đã gồm .NET runtime và SQLite) và `CHTL Manager (demo).bat` để mở với dữ liệu mẫu.
- Cài trên máy mới: copy file `.exe` sang (USB, Google Drive...) và nhấp đúp để chạy. Không cần cài .NET hay Visual Studio. Windows SmartScreen có thể cảnh báo vì file chưa ký số: chọn "More info" → "Run anyway".
- Lần mở đầu mất vài giây để giải nén và tạo CSDL; các lần sau nhanh hơn.
- Cập nhật phiên bản: thay file `.exe` mới, CSDL trong `%LocalAppData%` được giữ nguyên và tự sao lưu trước khi đổi cấu trúc. Sản phẩm cũ chưa có giá vốn: khai báo ở tab Quản lý Kho, lọc "Chưa có giá vốn", nút "Giá vốn...".

Tài khoản ban đầu là `admin` / `admin123`. Chế độ `--demo` có thêm `thungan` / `123456`.

Khi dùng thật, hãy đổi mật khẩu mặc định ngay (nút **Đổi mật khẩu** trên thanh tiêu đề).

## Cơ sở dữ liệu

- Lần chạy đầu, ứng dụng tự tạo file `%LocalAppData%\CHTLManager\chtlmanager.db` với tài khoản Admin, không có dữ liệu giả. Chạy với tham số `--demo` để có 12 sản phẩm và khoảng 20 hóa đơn mẫu.
- **Nâng cấp phiên bản:** dữ liệu được giữ nguyên. Trước khi đổi cấu trúc, ứng dụng tự sao lưu thành `chtlmanager.backup-*.db` cùng thư mục, giữ 5 bản gần nhất.
- CSDL nằm trong thư mục của tài khoản Windows đang dùng: mỗi máy, mỗi tài khoản Windows có một CSDL riêng.
- **Sao lưu:** đóng ứng dụng bằng nút đóng cửa sổ, rồi copy file `chtlmanager.db` ra nơi khác. Nếu cạnh đó còn file `chtlmanager.db-wal` (ứng dụng từng bị tắt đột ngột), dữ liệu mới nhất đang nằm trong file này: hãy mở ứng dụng rồi đóng lại bình thường, đến khi file đó biến mất mới copy.
- **Khôi phục:** tắt ứng dụng, xóa `chtlmanager.db-wal` và `chtlmanager.db-shm` nếu có, rồi chép file sao lưu trở lại thành `chtlmanager.db`.
- **Làm mới dữ liệu demo (không dùng cho cửa hàng thật):** tắt ứng dụng, xóa `chtlmanager.db` (cùng `-wal`, `-shm` nếu có), mở lại với `--demo`.

## Phạm vi và hạn chế

- Chạy trên **một máy**. SQLite là CSDL dạng file, phù hợp với một quầy, không cần cài máy chủ CSDL. Muốn nhiều máy dùng chung dữ liệu cần đổi sang SQL Server trong mạng LAN hoặc thêm Web API (chỉ thay tầng DAL nhờ kiến trúc phân tầng).
- Phân quyền thực hiện ở giao diện; CSDL không mã hóa.
- Cách xử lý chênh lệch đã lưu không đổi được (ví dụ đã chọn thất thoát nhưng đúng ra là đếm sai), nên Admin cần kiểm tra trước khi lưu.
- Chưa có: in hóa đơn, trả hàng từng phần, trả hàng cho nhà cung cấp, chi phí vận hành, sổ kế toán, bắt đổi mật khẩu mặc định, khuyến mãi.

Kịch bản demo và câu hỏi bảo vệ: xem [docs/DEMO.md](docs/DEMO.md).
