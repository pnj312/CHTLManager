# Kế hoạch hoàn thiện CHTLManager trước buổi bảo vệ (~12/11/2026)

## Context

Audit tại commit `fe9f942` cho thấy app chạy được, build sạch, nhưng:
- Có lỗi có thể lộ ra khi demo: nhập tiền sai (`100.000,50` thành 10.000.050), mã vạch bị kẹt sau khi xóa mềm, form Sửa ghi đè tồn kho, số lượng bị cắt ngầm, không có bắt lỗi toàn cục.
- Thiếu các màn hình giảng viên hay hỏi: lịch sử hóa đơn, doanh thu, quản lý tài khoản.

Mục tiêu: một bản demo **không có lỗi**, đủ các luồng chính, kèm kịch bản demo và câu trả lời bảo vệ. Ưu tiên tốc độ: bỏ in hóa đơn, phiếu nhập kho, Migrations (không thêm bảng mới), chạy nhiều máy.

**Quy trình mỗi phần:** code → build → kiểm chứng → báo cáo → **bạn duyệt** → commit (một commit cho mỗi phần). Push là một quyết định riêng.

---

## Phần 1 — Sửa lỗi demo (≈1,5 giờ) · commit `fix: ...`

| # | Việc | File |
|---|---|---|
| 1.1 | `MoneyParser.TryParse` chỉ nhận số nguyên dương. Cho phép dấu chấm hoặc phẩy **đúng vị trí phân cách hàng nghìn** (`100000`, `100.000`, `100,000`), khoảng trắng và đuôi `đ`. Từ chối số thập phân, số âm, chữ (`100.000,50`, `-5000`, `1,5tr`). | `CHTLManager.WPF/Helpers/MoneyParser.cs` |
| 1.2 | `ProductService.Add`: nếu mã vạch thuộc sản phẩm **đã ngừng kinh doanh** thì kích hoạt lại sản phẩm đó với thông tin mới, thay vì báo "đã tồn tại". `Update`: nếu trùng mã vạch với sản phẩm ngừng bán thì báo rõ lý do. | `CHTLManager.BLL/ProductService.cs` |
| 1.3 | `ProductService.Update` không ghi đè `Stock` nữa. Trên form: ô Tồn kho chỉ nhập được khi thêm mới, bị khóa khi đang chọn sản phẩm. Tồn kho chỉ đổi qua "Nhập thêm". | `ProductService.cs`, `Views/ProductUserControl.xaml(.cs)` |
| 1.4 | Giá bán phải lớn hơn 0. | `ProductService.Validate` |
| 1.5 | Giỏ hàng: báo "chỉ còn N trong kho" khi bấm + ở mức tối đa hoặc gõ số lượng vượt tồn (bắt ở `CellEditEnding`), thay vì cắt ngầm. | `Views/POSUserControl.xaml.cs`, `Models/CartItem.cs` |
| 1.6 | Bắt lỗi toàn cục `DispatcherUnhandledException`: hiện MessageBox, `e.Handled = true`, app không bị tắt. | `App.xaml.cs` |
| 1.7 | Lỗi ghi database (`DbUpdateException`, ví dụ trùng mã hóa đơn) ở màn hình thanh toán: hiện thông báo thân thiện và tải lại. | `POSUserControl.xaml.cs` |

**Checklist Phần 1**
- [x] Build: 0 lỗi, 0 cảnh báo
- [x] Harness BLL: thêm lại mã vạch đã xóa mềm thì sản phẩm hiện lại; Update không đổi tồn kho; giá 0 bị từ chối
- [x] Harness MoneyParser: các input hợp lệ ra đúng giá trị, các input sai đều bị từ chối
- [x] Bạn duyệt → commit (`22ef9b9`)

---

## Phần 2 — Tab "Hóa đơn & Doanh thu" (≈3–4 giờ) · commit `feat: order history and revenue`

**BLL** (`CHTLManager.BLL/OrderService.cs` + DTO mới `CHTLManager.BLL/Reports/RevenueReport.cs`)
- Dùng lại `GetHistory(from, to)` và `GetById` có sẵn.
- Thêm `RevenueReport GetRevenueReport(DateTime from, DateTime to)`: **tính tổng phía client** (SQLite không `Sum` hay `OrderBy` được `decimal`). Kết quả gồm: tổng doanh thu, số hóa đơn, giá trị trung bình mỗi hóa đơn, tổng số sản phẩm đã bán, doanh thu theo từng ngày, top 5 sản phẩm theo doanh thu.
- Thêm vào `IOrderService`.

**WPF** (`Views/OrderHistoryUserControl.xaml(.cs)` mới, sửa `Views/MainWindow.xaml(.cs)`)
- Hàng trên: DatePicker Từ/Đến (mặc định hôm nay), các nút nhanh Hôm nay / 7 ngày / Tháng này, nút Xem.
- Thẻ tổng quan: Doanh thu, Số hóa đơn, Trung bình mỗi hóa đơn, Số sản phẩm bán.
- Trái: DataGrid hóa đơn (Mã, Thời gian, Thu ngân, Tổng tiền). Phải: chi tiết hóa đơn đang chọn, gồm khách đưa và tiền thừa.
- Dưới: hai DataGrid "Doanh thu theo ngày" và "Top 5 sản phẩm". Không dùng thư viện biểu đồ.
- `MainWindow`: thêm TabItem, chỉ Admin thấy, gọi `Reload()` khi chọn tab (cùng cơ chế với `TabMain_SelectionChanged` hiện có).
- Kiểm tra "Từ" phải trước hoặc bằng "Đến".

**Hóa đơn mẫu** (`CHTLManager.BLL/DbInitializer.cs`)
- Chỉ nạp khi `Orders` đang trống. Tạo khoảng 25 hóa đơn trong 7 ngày gần nhất, random với seed cố định để kết quả luôn giống nhau, chia cho cả hai tài khoản.
- Mã `HDyyyyMMdd-NNNN` theo từng ngày (tương thích với cách sinh mã đếm theo ngày).
- Trừ tồn kho tương ứng để số liệu nhất quán.

**Checklist Phần 2**
- [x] Build sạch
- [x] Harness: database mới có hóa đơn mẫu, tồn kho đã bị trừ đúng, mã không trùng
- [x] Harness: `GetRevenueReport` khớp với tổng tính tay; khoảng ngày rỗng trả về 0, không lỗi
- [x] Bán thêm một hóa đơn hôm nay: mã tiếp nối đúng (không trùng với hóa đơn mẫu)
- [x] Giao diện tab mới hiển thị đúng (kiểm tra cùng lượt chạy thử ở Phần 3)
- [x] Bạn duyệt → commit (`bb25538`)

---

## Phần 3 — README, chạy thử toàn bộ, kịch bản demo (≈1,5 giờ) · commit `docs: ...`

- Viết lại `README.md`: yêu cầu hệ thống, cách chạy và publish, vị trí database, backup/reset database, tài khoản mẫu, phạm vi một máy. Bỏ dòng "chưa biên dịch".
- Thêm `docs/DEMO.md`: kịch bản demo theo thứ tự và các câu trả lời bảo vệ (lấy từ mục I của báo cáo audit, cập nhật theo chức năng mới).
- Cập nhật `CLAUDE.md` nếu kiến trúc thay đổi.
- **Chạy thử UI thật:** xóa database → mở app → đi qua toàn bộ checklist bên dưới. Đã chạy bằng Windows UI Automation (script PowerShell điền ô, bấm nút, gõ phím thật, đọc hộp thoại, chụp màn hình) do computer use chưa bật.

**Checklist demo end-to-end (database sạch)**
- [x] Mở app lần đầu: tự tạo database, hiện màn đăng nhập
- [x] Sai mật khẩu → báo lỗi, không crash
- [x] Đăng nhập `thungan`: chỉ thấy tab Bán hàng
- [x] Quét mã vạch đúng → vào giỏ; mã sai → cảnh báo
- [x] Tìm "hao hao" (không dấu) → ra "Mì Hảo Hảo"
- [x] Bấm + đến khi vượt tồn → có cảnh báo; gõ SL vượt tồn / 0 / chữ → có cảnh báo; quét hàng hết → cảnh báo
- [x] Nhập khách đưa `100.000` → tiền thừa đúng; `100.000,5` → báo không hợp lệ
- [x] Thanh toán thiếu tiền → bị chặn
- [x] Thanh toán đủ → hiện hóa đơn, giỏ trống, tồn kho giảm
- [x] Đăng xuất → quay lại đăng nhập
- [x] Đăng nhập `admin`: thấy đủ các tab
- [x] Thêm, sửa sản phẩm (ô tồn kho bị khóa khi sửa), nhập thêm kho
- [x] Xóa sản phẩm đã bán → ngừng kinh doanh; thêm lại cùng mã vạch → sản phẩm hiện lại
- [x] Tab Hóa đơn & Doanh thu: có số liệu mẫu; hóa đơn vừa bán xuất hiện; xem chi tiết; đổi khoảng ngày
- [x] Tài khoản (Phần 5): tạo thu ngân mới → đăng nhập được; khóa → không đăng nhập được
- [x] Không có lần nào app bị tắt đột ngột
- [x] Lỗi phát sinh đã sửa: nút `+`/`−` trong giỏ quá hẹp, không thấy ký hiệu
- [x] Bạn duyệt → commit (`301a9e5`)

---

## Phần 3b — Bán khi kho báo hết + Kiểm kê (bổ sung, ≈1,5 giờ) · commit `feat: oversell with confirmation and stock count`

Lý do: thực tế cửa hàng tiện lợi, số tồn trên phần mềm thường lệch hàng trên kệ (nhập chưa ghi, đếm sai, mất, hỏng). Chặn bán khi kho báo hết làm mất doanh thu ngay trước mặt khách.

- Thu ngân bấm `+` / quét / gõ SL vượt tồn → hộp thoại xác nhận (mặc định No). Yes → dòng đánh dấu "(vượt tồn)", được bán.
- `OrderService.CreateOrder(..., allowOversellProductIds)`: chỉ sản phẩm đã xác nhận mới được bán vượt; tồn kho có thể âm.
- Hóa đơn nhắc "kho đang âm, cần kiểm kê"; tồn âm hiện đỏ ở cả tab Bán hàng và Kho; bộ lọc "Chỉ hiện SP cần kiểm kê".
- `ProductService.SetCountedStock` + mục **Kiểm kê** trong tab Kho: nhập số đếm thực tế, hiện chênh lệch, xác nhận.

**Checklist Phần 3b**
- [x] Build sạch
- [x] Harness 17/17: không xác nhận thì vẫn chặn; xác nhận SP này không mở khóa SP khác; tồn âm; nhập kho/kiểm kê khi âm; sửa SP tồn âm được
- [x] Lỗi phát hiện và đã sửa: sản phẩm tồn âm không sửa được giá/tên (kiểm tra "tồn ≥ 0" chạy cả khi Sửa)
- [x] UI thật: hỏi xác nhận khi vượt tồn (No giữ nguyên, Yes bán được), gõ SL vượt tồn hỏi xác nhận, hóa đơn có lưu ý, tồn âm màu đỏ, lọc + kiểm kê đưa tồn về số thực tế
- [x] README, DEMO (kịch bản 2b + câu hỏi 13–14), CLAUDE.md cập nhật
- [x] Bạn duyệt → commit

---

## Phần 3c — Nhật ký kho, thất thoát, cách nói dễ hiểu (bổ sung, ≈3 giờ) · commit `feat: stock log and clearer out-of-book wording`

Lý do: kho báo còn mà không thấy hàng là thất thoát thật, nhưng kiểm kê cũ ghi đè số tồn không để lại dấu vết. Số âm và chữ "vượt tồn" cũng khó hiểu.

- Bảng `StockMovements`: nhập kho, kiểm kê, khai báo ban đầu, bán ngoài sổ; không có chức năng sửa/xóa.
- Cửa sổ Kiểm kê: phần mềm ghi / đếm được, kết luận chênh lệch, bắt buộc lý do đúng chiều.
- Tab Nhật ký kho: lọc ngày, tổng thất thoát theo giá bán, số lượng bán ngoài sổ.
- Không còn số âm trên màn hình: `0 (bán ngoài sổ: N)`, "(kho chưa ghi)", lời nhắc mới tại quầy và khi nhập bù.
- Bổ sung: sản phẩm có nhật ký chỉ ngừng kinh doanh; CSDL cũ tự có bảng mới.

**Checklist Phần 3c**
- [x] Build sạch
- [x] Harness 24/24: ghi nhật ký đủ các loại; lý do sai chiều bị chặn; thất thoát không tính "Đếm sai trước đó"; nâng cấp CSDL cũ giữ dữ liệu
- [x] UI thật: toàn bộ luồng mới + chạy lại luồng cũ (thu ngân, kho, doanh thu, tài khoản)
- [x] Lỗi phát hiện và đã sửa: lời nhắc ghi Yes/No trong khi Windows tiếng Việt hiện Có/Không; gõ SL vượt tồn rồi chọn Không bị nhảy lên bằng tồn kho
- [x] Bạn duyệt → commit

---

## Phần 4 — Bản cài đặt để demo (≈30 phút) · không commit file build

- Publish một file `.exe` self-contained:
  ```bash
  dotnet publish CHTLManager.WPF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
  ```
  Cờ `IncludeNativeLibrariesForSelfExtract` cần cho SQLite native; nén giảm file từ 169 MB xuống 71 MB.
- Chạy thử file `.exe` vừa tạo.
- Thêm `publish/` vào `.gitignore`.

**Checklist Phần 4**
- [x] File `.exe` tự chạy từ thư mục riêng (chỉ 1 file), tự tạo database, đăng nhập, bán hàng, xem doanh thu được. Mở lần đầu 6,7 giây, lần sau 3,1 giây
- [x] Ghi kích thước file và cách copy sang máy demo vào README
- [ ] Chưa thử trên máy khác thật (chỉ thử trên máy này, file self-contained nên không phụ thuộc .NET đã cài)
- [x] Bạn duyệt → commit (`.gitignore`, README, PLAN; không commit file build)

---

## Phần 5 — Quản lý tài khoản (tùy chọn, làm sau cùng, ≈2–3 giờ) · commit `feat: user management`

**BLL** (`CHTLManager.BLL/UserService.cs`, `Interfaces/IUserService.cs`)
- `SetActive(int userId, bool active, int currentUserId)`: không cho tự khóa chính mình, không cho khóa Admin đang hoạt động cuối cùng.
- `ResetPassword(int userId, string newPassword)` (Admin dùng).
- `ChangePassword(int userId, string oldPassword, string newPassword)`: kiểm tra mật khẩu cũ bằng `PasswordHasher.Verify` có sẵn.
- Tối thiểu 6 ký tự, dùng lại quy tắc đang có trong `Create`.

**WPF**
- `Views/UserUserControl.xaml(.cs)`: tab "Tài khoản" (chỉ Admin). DataGrid (Tên đăng nhập, Họ tên, Vai trò, Trạng thái); form tạo mới (PasswordBox, ComboBox vai trò); các nút Khóa/Mở khóa và Đặt lại mật khẩu.
- `Views/ChangePasswordWindow.xaml(.cs)`: nút "Đổi mật khẩu" trên header `MainWindow`, mọi vai trò đều dùng được.

**Checklist Phần 5**
- [x] Harness 17/17: tạo, khóa, mở khóa, đặt lại, đổi mật khẩu; chặn tự khóa và chặn khóa Admin cuối cùng; tài khoản khóa không tạo được hóa đơn
- [x] UI: tạo thu ngân → đăng nhập; khóa → báo "đã bị khóa"; tự khóa bị chặn; đặt lại + đổi mật khẩu → mật khẩu cũ không còn dùng được
- [x] Chạy lại toàn bộ checklist demo ở Phần 3 (kể cả bán vượt tồn): đạt
- [x] Bạn duyệt → commit

---

## Lịch đề xuất

| Ngày | Việc |
|---|---|
| Ngày 1 | Phần 1, Phần 2 |
| Ngày 2 | Phần 3, Phần 4 |
| Ngày 3 | Phần 5 + chạy lại checklist demo |
| Còn lại | Buffer sửa lỗi phát sinh, bạn diễn tập theo `docs/DEMO.md` |

## Cách kiểm chứng chung

- `dotnet build CHTLManager.slnx` sau mỗi phần.
- Harness console trong scratchpad gọi BLL thật trên database tạm (gán `AppDbContext.DbPath` sang file tạm), không đụng database thật.
- Chạy thử UI thật ở Phần 3 và Phần 5.
- Trước buổi demo: xóa `%LocalAppData%\CHTLManager\chtlmanager.db` để có dữ liệu mẫu sạch.

## Ngoài phạm vi (trả lời bằng lời khi bảo vệ)

In hóa đơn, phiếu nhập kho có lịch sử, Migrations, chạy nhiều máy (SQL Server hoặc API), khuyến mãi, trả hàng.
