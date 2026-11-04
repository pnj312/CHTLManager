# Kịch bản demo và câu hỏi bảo vệ

## Chuẩn bị (trước buổi bảo vệ)

1. Tắt ứng dụng nếu đang mở.
2. Xóa file `%LocalAppData%\CHTLManager\chtlmanager.db` để có dữ liệu mẫu sạch. Mở thư mục nhanh: nhấn `Win + R`, gõ `%LocalAppData%\CHTLManager`.
3. Mở ứng dụng **trong khoảng 7:00–21:00** để hóa đơn mẫu có cả hóa đơn của ngày hôm đó.
4. Để sẵn danh sách mã vạch dưới đây (copy/dán thay cho máy quét).

| Sản phẩm | Mã vạch | Ghi chú |
|---|---|---|
| Mì Hảo Hảo tôm chua cay | `8934563138165` | Giá 4.500 |
| Coca-Cola lon 330ml | `8934588012105` | Giá 10.000 |
| Dầu gội Clear 330g | `8934868150012` | Tồn thấp (xem cột Tồn, thường khoảng 5) → demo "kho báo hết nhưng kệ còn hàng" |
| Mã không tồn tại | `123` | Demo cảnh báo |

## Kịch bản (khoảng 7–10 phút)

### 1. Đăng nhập và phân quyền
- Gõ sai mật khẩu → hiện "Sai tên đăng nhập hoặc mật khẩu".
- Đăng nhập `thungan / 123456` → chỉ có tab **Bán hàng**.

### 2. Bán hàng (thu ngân)
- Dán mã `8934563138165` vào ô quét, nhấn Enter → Mì Hảo Hảo vào giỏ. Enter thêm lần nữa → số lượng tăng lên 2.
- Dán mã `123` → cảnh báo không tìm thấy.
- Gõ `coca` (hoặc `hao hao` không dấu) vào ô tìm theo tên → nhấp đúp để thêm vào giỏ.
- Nhập khách đưa `100.000,5` → tiền thừa không tính, bấm Thanh toán → báo không hợp lệ.
- Nhập khách đưa `10.000` (thiếu) → bấm Thanh toán → báo còn thiếu.
- Nhập khách đưa `200.000` → Thanh toán → hiện hóa đơn với mã `HDyyyyMMdd-NNNN`, giỏ trống, cột Tồn giảm đúng số lượng vừa bán.

### 2b. Kho báo hết nhưng kệ vẫn còn hàng (điểm nhấn nghiệp vụ)
- Kể tình huống: "Phần mềm báo Dầu gội chỉ còn N chai, nhưng khách cầm lên chai thứ N+1 vì lô nhập hôm qua chưa được ghi."
- Thêm Dầu gội, bấm `+` đến bằng số tồn, bấm thêm lần nữa → hộp thoại **"Kho báo không đủ hàng... Vẫn bán?"** (mặc định No để tránh bấm nhầm).
- Chọn **Yes** → dòng hiện "(vượt tồn)", bán được bình thường. Thanh toán → hóa đơn ghi đủ, cuối hóa đơn nhắc "kho đang âm, Admin cần kiểm kê"; cột Tồn hiện **-1** màu đỏ.
- Đăng xuất.

### 3. Quản lý kho (admin)
- Đăng nhập `admin / admin123` → có đủ 3 tab.
- Tab **Quản lý Kho**: thêm sản phẩm mới (mã vạch, tên, giá, tồn).
- Chọn một sản phẩm, sửa giá → ô Tồn kho bị khóa (giải thích: tồn kho chỉ thay đổi qua nhập kho hoặc bán hàng, tránh ghi đè số liệu).
- Nhập kho nhanh: nhập `20` → tồn kho tăng 20.
- Xóa Mì Hảo Hảo (đã có trong hóa đơn) → chuyển sang ngừng kinh doanh, lịch sử vẫn giữ. Thêm lại cùng mã vạch → sản phẩm được kinh doanh lại.
- Tích **"Chỉ hiện SP cần kiểm kê"** → chỉ còn Dầu gội (tồn -1, màu đỏ). Chọn nó, nhập số đếm thực tế (ví dụ `10`) vào **Kiểm kê** → hộp thoại hiện tồn trên phần mềm, số đếm và chênh lệch → Yes → tồn kho về 10, hết đỏ.

### 4. Hóa đơn và doanh thu (admin)
- Tab **Hóa đơn & Doanh thu**: mặc định 7 ngày, có sẵn số liệu mẫu.
- Hóa đơn vừa bán ở bước 2 nằm đầu danh sách; bấm vào để xem chi tiết, tiền khách đưa, tiền thừa.
- Bấm **Hôm nay** / **Tháng này** để đổi khoảng thời gian; xem doanh thu theo ngày và top 5 sản phẩm.

### 5. Tài khoản (admin)
- Tab **Tài khoản**: tạo thu ngân mới `thungan2`, mật khẩu `abc123` (thử mật khẩu `123` trước → báo phải ít nhất 6 ký tự).
- Chọn `thungan2` → **Khóa tài khoản** → trạng thái "Đã khóa" màu đỏ. Chọn `admin` → Khóa → báo không thể tự khóa.
- Đăng xuất, đăng nhập `thungan2 / abc123` → "Tài khoản đã bị khóa. Vui lòng liên hệ Admin."
- Đăng nhập lại admin, mở khóa `thungan2`. Nút **Đổi mật khẩu** trên thanh tiêu đề: nhập sai mật khẩu hiện tại → báo lỗi.

## Câu hỏi bảo vệ và câu trả lời

1. **Dự án giải quyết bài toán gì?** Tính tiền bằng mã vạch, tự trừ tồn kho, lưu hóa đơn và thống kê doanh thu cho cửa hàng tiện lợi một quầy, thay cho ghi sổ và tính tay.
2. **Đối tượng sử dụng?** Admin (chủ cửa hàng/quản lý) và Thu ngân. Khách hàng không thao tác trên phần mềm.
3. **Tại sao xây dựng?** Giảm sai sót khi tính tiền, biết tồn kho chính xác theo thời gian thực, có số liệu doanh thu để ra quyết định nhập hàng.
4. **Chạy một máy hay nhiều máy?** **Một máy.** Dữ liệu là một file SQLite trên máy đó.
5. **Database lưu ở đâu?** `%LocalAppData%\CHTLManager\chtlmanager.db`, tự tạo khi mở ứng dụng lần đầu.
6. **Tại sao SQLite?** Không cần cài máy chủ CSDL, cả CSDL là một file nên dễ cài đặt và sao lưu; đủ cho một quầy. Nhược điểm: không phù hợp để nhiều máy cùng ghi qua mạng.
7. **Muốn nhiều nhân viên cùng dùng?** Trên một máy: đăng xuất, đăng nhập tài khoản khác. Nhiều máy: chuyển CSDL sang SQL Server trong mạng LAN (chỉ đổi provider ở tầng DAL), hoặc xây Web API làm trung gian để phân quyền ở server.
8. **Cài đặt trên máy mới?** Cài .NET 8 Desktop Runtime và chạy file `.exe`, hoặc dùng bản publish self-contained không cần cài .NET. CSDL và dữ liệu mẫu tự tạo.
9. **Quy trình sử dụng?** Đăng nhập → quét hàng → nhập tiền khách đưa → thanh toán. Admin quản lý sản phẩm, nhập kho và xem doanh thu.
10. **Admin và Thu ngân có quyền gì?** Thu ngân bán hàng và tự đổi mật khẩu. Admin bán hàng, quản lý kho (nhập kho, kiểm kê), xem hóa đơn và doanh thu, quản lý tài khoản (tạo, khóa/mở khóa, đặt lại mật khẩu). Có chốt chặn: không thể tự khóa mình, không thể khóa Admin cuối cùng, nên hệ thống không bao giờ bị khóa hết quyền quản trị.
11. **Hạn chế hiện tại?** Chỉ chạy một máy; phân quyền ở giao diện; CSDL không mã hóa; chưa có in hóa đơn, phiếu nhập kho và lịch sử kiểm kê, trả hàng, khuyến mãi.
12. **Triển khai thực tế cần cải tiến gì?** CSDL máy chủ hoặc API cho nhiều quầy, phân quyền ở server, in hóa đơn, phiếu nhập kho và kiểm kê, sao lưu tự động, quản lý ca làm việc.

13. **Kho báo hết nhưng kệ vẫn còn hàng thì có bán được không?** Có. Số trên phần mềm có thể lệch thực tế (nhập hàng chưa ghi, đếm sai). Thu ngân xác nhận "vẫn bán" thì hóa đơn vẫn ghi đầy đủ, không mất doanh thu; tồn kho xuống âm và hiện đỏ để Admin biết số liệu đang lệch. Admin đếm hàng thực tế rồi dùng **Kiểm kê** đặt lại tồn kho. Kiểm kê cũng xử lý chiều ngược lại: hàng mất, hỏng làm số thực tế ít hơn phần mềm.
14. **Vậy có bị bán nhầm khi hết hàng thật không?** Không tự động: mỗi sản phẩm phải được thu ngân xác nhận riêng, hộp thoại mặc định chọn No, và BLL chỉ cho bán vượt tồn đúng những sản phẩm đã được xác nhận; sản phẩm khác trong cùng hóa đơn mà thiếu hàng thì cả hóa đơn vẫn bị chặn.

### Câu hỏi kỹ thuật có thể gặp
- **Làm sao trừ kho an toàn?** `OrderService.CreateOrder` kiểm tra tồn kho, trừ kho và lưu hóa đơn trong **một transaction**; lỗi ở bất kỳ bước nào thì hủy toàn bộ, kho không bị trừ.
- **Giá có bị sửa từ giao diện được không?** Không. Khi thanh toán, giá được đọc lại từ CSDL, không dùng giá giao diện gửi lên.
- **Mật khẩu lưu thế nào?** Băm PBKDF2-SHA256, 100.000 vòng, có salt ngẫu nhiên; không lưu mật khẩu gốc.
- **Có bị SQL Injection không?** Không: mọi truy vấn qua Entity Framework LINQ, tham số hóa tự động, không có SQL nối chuỗi.
- **Xóa sản phẩm đã bán thì lịch sử ra sao?** Chỉ ngừng kinh doanh (xóa mềm), hóa đơn cũ vẫn hiển thị đúng sản phẩm.
