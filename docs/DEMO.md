# Kịch bản demo và câu hỏi bảo vệ

## Chuẩn bị (trước buổi bảo vệ)

1. Tắt ứng dụng nếu đang mở.
2. Xóa file `%LocalAppData%\CHTLManager\chtlmanager.db` để có dữ liệu mẫu sạch. Mở thư mục nhanh: nhấn `Win + R`, gõ `%LocalAppData%\CHTLManager`.
3. Mở ứng dụng với tham số `--demo`, bằng file `CHTL Manager (demo).bat` đi kèm hoặc lệnh `CHTLManager.WPF.exe --demo`. Không có tham số này, app chỉ có tài khoản Admin như một cửa hàng thật.
4. Để sẵn danh sách mã vạch dưới đây (copy/dán thay cho máy quét). Trong app, chọn một sản phẩm rồi nhấn `Ctrl + C` hoặc chuột phải để chép mã vạch.

| Sản phẩm | Mã vạch | Ghi chú |
|---|---|---|
| Mì Hảo Hảo tôm chua cay | `8934563138165` | Giá 4.500 |
| Coca-Cola lon 330ml | `8934588012105` | Giá 10.000 |
| Dầu gội Clear 330g | `8934868150012` | Tồn 5: dùng cho tình huống kho báo hết |
| Mã không tồn tại | `123` | Demo cảnh báo |

### Dữ liệu mẫu

Mọi con số đều cố định, mở app giờ nào cũng giống nhau. Cửa hàng mở với kệ trống cách đây 7 ngày; toàn bộ tồn kho đều từ 6 phiếu nhập của 4 nhà cung cấp, không có số tồn nào thiếu chứng từ. Có 25 hóa đơn trong 7 ngày, doanh thu `1.300.000 đ`, hôm nay có 2 hóa đơn.

| Sản phẩm | Giá bán | Giá vốn | Tồn |
|---|---:|---:|---:|
| Mì Hảo Hảo tôm chua cay | 4.500 | 3.435 | 130 |
| Mì Omachi sườn hầm | 8.000 | 5.600 | 54 |
| Nước suối Aquafina 500ml | 6.000 | 3.800 | 107 |
| Coca-Cola lon 330ml | 10.000 | 7.068 | 130 |
| Trà xanh 0 độ 455ml | 10.000 | 6.800 | 66 |
| Sữa tươi Vinamilk 180ml | 8.500 | 6.500 | 77 |
| Bánh Oreo vani 133g | 16.000 | 11.500 | 34 |
| Snack Oishi tôm cay 40g | 7.000 | 5.056 | 68 |
| Cà phê G7 3in1 (hộp 18 gói) | 52.000 | 40.000 | 22 |
| Dầu gội Clear 330g | 98.000 | 72.000 | 5 |
| Khăn giấy Pulppy 2 lớp | 12.000 | 8.500 | 45 |
| Bút bi Thiên Long TL-027 | 4.000 | 2.500 | 195 |

| Nhà cung cấp | Tiền hàng | Đã trả | Còn nợ |
|---|---:|---:|---:|
| NPP Đồ uống | 1.963.200 | 1.000.000 | 963.200 |
| NPP Thực phẩm | 3.268.000 | 500.000 | 2.768.000 |
| NPP Tạp hóa | 925.000 | 0 | 925.000 |
| NPP Unilever | 720.000 | 720.000 | 0 |

Có sẵn các tình huống: hóa đơn `TP-2002` ghi 50 gói mì nhưng chỉ nhận 48 (chỉ nợ theo số thực nhận), giá Coca và mì tăng ở lô sau (giá vốn bình quân thay đổi), ba khoản thất thoát, và một khoản bồi thường `17.000 đ` đã thu `10.000 đ`.

## Kịch bản (khoảng 10–12 phút)

### 1. Đăng nhập và phân quyền
- Gõ sai mật khẩu → "Sai tên đăng nhập hoặc mật khẩu".
- Đăng nhập `thungan / 123456` → chỉ có tab **Bán hàng** và nút **Nhận hàng...** trên thanh tiêu đề. Thu ngân không thấy giá vốn.

### 2. Bán hàng
- Dán mã `8934563138165`, nhấn Enter hai lần → Mì Hảo Hảo số lượng 2. Dán mã `123` → cảnh báo.
- Gõ `hao hao` không dấu ở ô tìm → ra đúng sản phẩm.
- Khách đưa `100.000,5` → báo không hợp lệ. Khách đưa `1.000` → báo còn thiếu. Khách đưa `20.000` → hiện hóa đơn, cột Tồn giảm.

### 3. Kho báo hết nhưng khách vẫn cầm được hàng
- Tình huống: "Phần mềm báo Dầu gội còn 5, nhưng khách cầm chai thứ 6 vì lô nhập trước chưa được ghi."
- Thêm Dầu gội, bấm `+` tới 5, bấm thêm → **"Phần mềm ghi... chỉ còn 5, nhưng khách đang cầm sản phẩm này?"**, mặc định Không.
- Chọn **Có** → dòng hiện "(kho chưa ghi)". Thanh toán → hóa đơn nhắc "đã bán 1 cái ngoài sổ, cần kiểm kê"; cột Tồn hiện `0 (bán ngoài sổ: 1)` màu đỏ. Bán ngoài sổ tối đa 10 cái mỗi dòng.

### 4. Chỉ có thu ngân ở quán, nhà cung cấp giao hàng
- Bấm **Nhận hàng...**: nhà cung cấp `NPP Unilever`, số hóa đơn `MOI-1`. Ở ô thêm sản phẩm, gõ `dau goi` rồi chọn trong danh sách hiện ra; thử gõ tên nhà cung cấp hơi khác đi, ví dụ `NPP Unilevr`, để thấy app hỏi lại khi lưu.
- App báo còn hàng bán ngoài sổ từ lô trước, mở cửa sổ **Kiểm kê**. Thu ngân đếm hàng cũ **trước khi xếp hàng mới lên kệ**: nhập `2` → tồn 2, chênh lệch +3 chờ Admin.
- Quay lại phiếu: SL hóa đơn `10`, thực nhận `10`, không có giá → lưu. Tồn **12**, bán tiếp được. Đăng xuất.

### 5. Admin về đối chiếu
- Đăng nhập `admin / admin123`. Nút đỏ trên thanh tiêu đề: **1 phiếu chờ xác nhận, 1 chênh lệch chờ xử lý**.
- Bấm nút → tab **Nhập hàng**: đối chiếu với tờ hóa đơn, **Xác nhận phiếu**, nhập giá `75.000`.
- Bấm nút lần nữa → tab **Chênh lệch kho**: chọn dòng Dầu gội → **Xử lý...** → **Lô hàng chưa ghi**: `NPP Unilever`, hóa đơn `CU-9`, giá `70.000`.
- Tab **Công nợ NCC**: NPP Unilever trước đó hết nợ, giờ nợ `960.000 đ` (3×70.000 + 10×75.000). Bấm **Trả tiền...**, trả `500.000`; thử trả quá số nợ → bị chặn.

### 6. Kho báo còn nhưng không tìm thấy hàng (thất thoát)
- Tab **Quản lý Kho**: chọn Coca, **Kiểm kê...**, nhập ít hơn sổ 3 cái → tồn giảm ngay, chênh lệch chờ xử lý.
- Tab **Chênh lệch kho** → **Xử lý...** → **Có người chịu trách nhiệm bồi thường**: `Thu ngân 01`, số tiền `20.000`. Sau đó **Thu bồi thường...** `5.000` → còn phải thu `15.000`.
- Giải thích: "Đếm sai" vẫn chọn được nhưng phải ghi chú và hiện thẻ riêng; người đếm tự xử lý thì có nhãn "Tự xử lý".

### 7. Lợi nhuận và sửa sai
- Tab **Hóa đơn & Doanh thu**: Doanh thu, Giá vốn, **Lãi gộp**, dòng thất thoát và **Lãi sau chênh lệch kho**, lợi nhuận theo sản phẩm. Dòng đỏ "N dòng giá vốn tạm tính" nhắc phần bán ngoài sổ.
- Chọn hóa đơn vừa bán, **Hủy hóa đơn...**, lý do `Quét nhầm` → hàng về kho, doanh thu giảm, hóa đơn vẫn còn và đánh dấu đã hủy.
- Tab **Nhật ký kho**: mọi thay đổi tồn kho ngoài bán thường, có người làm và chứng từ.

### 8. Quản lý và tài khoản (nếu còn thời gian)
- Tab **Quản lý Kho**: xóa sản phẩm còn hàng → bị chặn, yêu cầu bán hết hoặc kiểm kê về 0, để không ai xóa rồi thêm lại mà che số lệch.
- Tab **Tài khoản**: tạo thu ngân, khóa tài khoản; tự khóa mình → bị chặn.

## Câu hỏi bảo vệ và câu trả lời

1. **Dự án giải quyết bài toán gì?** Cửa hàng tiện lợi một quầy cần tính tiền nhanh bằng mã vạch và biết chính xác: còn bao nhiêu hàng, lãi bao nhiêu, nợ nhà cung cấp bao nhiêu, mất hàng do đâu.
2. **Đối tượng sử dụng?** Admin (chủ, quản lý) và Thu ngân. Khách hàng không thao tác trên phần mềm.
3. **Chạy một máy hay nhiều máy?** **Một máy.** Dữ liệu là một file SQLite trên máy đó, riêng cho từng tài khoản Windows.
4. **Database lưu ở đâu?** `%LocalAppData%\CHTLManager\chtlmanager.db`, tự tạo khi mở lần đầu.
5. **Tại sao SQLite?** Không cần cài máy chủ, cả dữ liệu là một file nên dễ cài và sao lưu; đủ cho một quầy. Nhược điểm: không phù hợp để nhiều máy cùng ghi qua mạng.
6. **Muốn nhiều nhân viên cùng dùng?** Trên một máy: đăng xuất, đăng nhập tài khoản khác. Nhiều máy: chuyển sang SQL Server trong mạng LAN (đổi provider ở tầng DAL), hoặc xây Web API để phân quyền ở server.
7. **Cài đặt trên máy mới?** Copy một file `.exe` và chạy, không cần cài .NET. Cơ sở dữ liệu tự tạo.
8. **Nâng cấp có mất dữ liệu không?** Không. App dùng EF Migrations, tự sao lưu `chtlmanager.backup-*.db` trước khi đổi cấu trúc và giữ 5 bản gần nhất. Bản cũ chưa có Migrations cũng được nhận diện và nâng cấp. Đã thử thật từ bản cũ.
9. **Admin và Thu ngân có quyền gì?** Thu ngân bán hàng, nhận hàng (không thấy giá), đếm hàng khi nhận, đổi mật khẩu của mình. Admin làm thêm: sản phẩm, xác nhận phiếu, công nợ, xử lý chênh lệch, báo cáo, hủy hóa đơn, tài khoản.

### Về giá vốn và lợi nhuận

10. **Giá vốn tính thế nào?** Bình quân gia quyền tức thời: `(Tồn × Giá vốn cũ + SL nhập × Giá nhập) ÷ (Tồn + SL nhập)`, tính lại sau mỗi phiếu nhập có giá. Giá vốn lưu vào từng dòng hóa đơn lúc bán, nên báo cáo cũ không đổi khi giá nhập thay đổi.
11. **Lợi nhuận trong app có phải lợi nhuận thật không?** Là **lãi gộp sau chênh lệch kho**: Doanh thu − Giá vốn − Thất thoát + Thu nhập khác + Bồi thường đã thu. **Chưa trừ** lương, mặt bằng, điện nước. Không thay thế sổ kế toán và báo cáo thuế.
12. **Bán khi chưa có giá vốn thì sao?** Dòng đó đánh dấu "tạm tính", lấy giá vốn hiện có hoặc giá nhập gần nhất, nếu không có thì là 0. Báo cáo hiện số dòng tạm tính để người xem biết độ tin cậy. Hóa đơn đã bán không bị sửa lại giá vốn.

### Về chênh lệch kho

13. **Kho báo hết mà khách vẫn cầm được hàng?** Vẫn bán được sau khi thu ngân xác nhận, tối đa 10 cái vượt tồn mỗi dòng. Bên trong sổ ghi -N: đây là số sổ sách, không phải số hàng trên kệ, để khi kiểm kê tính đúng quy mô lô chưa ghi: bán 1 ngoài sổ, đếm còn 2 thì lô đó là 3 cái.
14. **Nhà cung cấp giao lô mới khi còn bán ngoài sổ?** App bắt đếm hàng cũ trước, rồi mới nhập, để số nhập khớp đúng tờ hóa đơn: lô cũ +3, lô mới +10, tồn 12. Điểm này phụ thuộc người làm: phải đếm trước khi xếp hàng mới lên kệ.
15. **Kho báo còn mà không thấy hàng?** Kiểm kê hai bước. Bước đếm làm tồn kho đúng ngay. Bước xử lý quyết định con số đi về đâu:

    | Trường hợp | Cách xử lý | Con số đi về đâu |
    |---|---|---|
    | Thiếu | Mất, hỏng, hết hạn | Chi phí thất thoát theo giá vốn |
    | Thiếu | Có người bồi thường | Chi phí thất thoát, cộng khoản phải thu và các lần thu |
    | Thiếu | Đếm sai | Không vào chi phí, hiện thẻ riêng, bắt buộc ghi chú |
    | Thừa | Lô chưa ghi | Tạo phiếu nhập, sinh công nợ, không cộng kho lần hai |
    | Thừa | Không rõ nguồn | Thu nhập khác theo giá vốn |
    | Thừa | Đếm sai | Như dòng đếm sai ở trên |

    Cách tách hai bước này theo thực hành kế toán: ghi nhận "chờ xử lý" trước, rồi mới quyết định.
16. **Nhận thiếu so với hóa đơn có tính thất thoát không?** Không. Đó là việc với nhà cung cấp: hóa đơn 10, nhận 9, giá 100 thì nợ 900 và phiếu hiện "nhận thiếu 1".
17. **Nhân viên có che giấu mất hàng được không?** Khó hơn nhưng không tuyệt đối. Không xóa được sản phẩm còn tồn; nhật ký không có chức năng sửa, xóa; "Đếm sai" phải ghi chú và hiện thẻ riêng; ai tự đếm tự xử lý đều có nhãn. Tuy nhiên người mở trực tiếp file database vẫn sửa được, và Admin duy nhất vẫn tự chọn được cách xử lý, nên chủ cửa hàng phải xem báo cáo định kỳ.
18. **Thu ngân khai khống khi nhận hàng?** Có thể, vì tồn kho tăng ngay để bán được. Kiểm soát bằng tên người nhập, số hóa đơn và bước Admin đối chiếu; từ chối phiếu thì tồn kho bị trừ lại.
19. **Phần mềm có tự biết mất hàng không?** Không. Sổ vẫn sai cho đến khi có người đếm, nên cửa hàng phải kiểm kê định kỳ.

### Hạn chế và hướng phát triển

20. **Hạn chế hiện tại?** Chỉ chạy một máy; phân quyền ở giao diện, chưa kiểm tra ở tầng nghiệp vụ; database không mã hóa; chưa bắt đổi mật khẩu mặc định; chưa in hóa đơn; chưa có trả hàng từng phần, trả hàng cho nhà cung cấp, chi phí vận hành, sổ kế toán; chưa hủy được lần thu bồi thường ghi nhầm.
21. **Triển khai thực tế cần gì thêm?** In hóa đơn, thử với máy quét thật, phân quyền ở tầng nghiệp vụ, kiểm kê nhiều sản phẩm một lúc, chi phí vận hành, máy chủ dữ liệu nếu có nhiều quầy.

### Câu hỏi kỹ thuật có thể gặp
- **Làm sao trừ kho an toàn?** `OrderService.CreateOrder` kiểm tra, trừ kho và lưu hóa đơn trong **một transaction**; lỗi ở bất kỳ bước nào thì hủy toàn bộ.
- **Giá có bị sửa từ giao diện được không?** Không. Khi thanh toán, giá và giá vốn được đọc lại từ CSDL.
- **Mật khẩu lưu thế nào?** Băm PBKDF2-SHA256, 100.000 vòng, có salt ngẫu nhiên.
- **Có bị SQL Injection không?** Dữ liệu người dùng luôn qua Entity Framework có tham số hóa. SQL viết tay chỉ dùng cho nâng cấp cấu trúc và không chứa dữ liệu người dùng.
- **Hàng thu ngân nhận mà chưa có giá thì tính giá vốn thế nào?** Hàng đó vào tồn kho ngay nhưng chưa vào giá vốn bình quân; giá chỉ được tính lúc Admin xác nhận. Nếu bán vào phần hàng chưa có giá, dòng đó được đánh dấu giá vốn tạm tính và báo cáo hiện rõ số dòng tạm tính.
- **Sản phẩm chưa có giá vốn mà bị thiếu hàng thì sao?** App không cho ghi thất thoát 0 đ: phải khai báo giá vốn trước, để báo cáo không che mất khoản mất thật.
- **Nhập hàng có tránh gõ sai được không?** Gõ mã hoặc tên không dấu là hiện danh sách để chọn, giá điền sẵn theo lần nhập gần nhất và tổng tiền hiện ngay để đối chiếu với hóa đơn. Tên nhà cung cấp gần giống tên đã có thì app hỏi lại, vì tạo trùng sẽ làm công nợ bị tách đôi. Đóng phiếu chưa lưu cũng được hỏi lại.
- **Nhập nhầm giá thì sửa thế nào?** Admin bấm **Sửa giá nhập** trên phiếu đã xác nhận và phải ghi lý do: công nợ và giá vốn đổi theo, tồn kho giữ nguyên, nhật ký kho ghi giá cũ, giá mới và người sửa. Không hủy rồi nhập lại, vì nếu hàng đã bán thì việc đó tạo ra tồn kho không có thật. Lãi của hóa đơn đã bán giữ nguyên vì giá vốn được chốt lúc bán; khoản lệch nếu có nằm ở các lần bán sau.
- **Ghi nhầm tiền bồi thường thì sao?** Admin mở **Các lần thu**, chọn lần sai và hủy kèm lý do. Lần đó vẫn nằm trong danh sách, ghi rõ ai hủy và vì sao, nhưng không còn tính vào số đã thu và báo cáo. Ghi nhầm người hoặc số tiền phải bồi thường thì dùng **Sửa khoản bồi thường**: phải có lý do và nhật ký kho ghi lại giá trị cũ, giá trị mới. Không đổi được người khi còn lần thu có hiệu lực, vì số tiền đó do người cũ trả. Giới hạn đã biết: cách xử lý chênh lệch đã chọn, ví dụ đã chọn Thất thoát nhưng đúng ra là Đếm sai, hiện chưa đảo lại được, nên Admin cần kiểm tra trước khi xác nhận.
- **Có kiểm thử không?** Có project xUnit cho tầng nghiệp vụ, chạy trên database tạm, gồm cả nâng cấp từ database cũ và đối chiếu dữ liệu mẫu với chứng từ. Giao diện được chạy thử bằng UI Automation, không nằm trong bộ test.
