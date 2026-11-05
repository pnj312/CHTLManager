# Kịch bản demo và câu hỏi bảo vệ

## Chuẩn bị (trước buổi bảo vệ)

1. Tắt ứng dụng nếu đang mở.
2. Xóa file `%LocalAppData%\CHTLManager\chtlmanager.db` để có dữ liệu mẫu sạch. Mở thư mục nhanh: nhấn `Win + R`, gõ `%LocalAppData%\CHTLManager`.
3. Mở ứng dụng với tham số `--demo` **trong khoảng 7:00–21:00** để có dữ liệu mẫu, gồm cả hóa đơn của ngày hôm đó: `CHTLManager.WPF.exe --demo`. Không có tham số này, app chỉ có tài khoản Admin như một cửa hàng thật.
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
- Thêm Dầu gội, bấm `+` đến bằng số tồn, bấm thêm lần nữa → hộp thoại **"Phần mềm ghi... chỉ còn N, nhưng khách đang cầm sản phẩm này?"** (mặc định Không để tránh bấm nhầm).
- Chọn **Có** → dòng hiện "(kho chưa ghi)", bán được bình thường. Thanh toán → hóa đơn ghi đủ, cuối hóa đơn nhắc "đã bán 1 cái ngoài sổ, cần kiểm kê"; cột Tồn hiện **`0 (bán ngoài sổ: 1)`** màu đỏ.
- Đăng xuất.

### 3. Quản lý kho (admin)
- Đăng nhập `admin / admin123` → có đủ 3 tab.
- Tab **Quản lý Kho**: thêm sản phẩm mới (mã vạch, tên, giá, tồn).
- Chọn một sản phẩm, sửa giá → ô Tồn kho bị khóa (giải thích: tồn kho chỉ thay đổi qua nhập kho hoặc bán hàng, tránh ghi đè số liệu).
- Chọn một sản phẩm bình thường, bấm **Nhập kho sản phẩm đang chọn...**: nhập số lượng `20`, nhà cung cấp, số hóa đơn → tồn kho tăng đúng 20.
- Xóa Mì Hảo Hảo khi còn hàng → bị chặn, app yêu cầu bán hết hoặc kiểm kê về 0 (để không ai dùng xóa rồi thêm lại mà che số lệch). Sản phẩm tồn 0 đã có lịch sử thì chuyển sang ngừng kinh doanh; thêm lại cùng mã vạch thì kinh doanh lại với tồn kho giữ nguyên.
- **Nhà cung cấp giao lô mới, lô cũ chưa chốt:** tích **"Chỉ hiện SP cần kiểm kê"** → chỉ còn Dầu gội. Chọn nó, bấm **Nhập kho** → app báo cần kiểm kê trước để hai lô không lẫn, rồi tự mở cửa sổ Kiểm kê. Nhập số đếm được `2`, chọn **Hàng nhập chưa ghi**, số hóa đơn cũ `0099` → tồn về 2. Cửa sổ Nhập kho tự mở: `10`, `NPP Unilever`, `0123` → tồn **12**. Bỏ tích ô lọc.
- **Tình huống ngược lại (thất thoát):** chọn Coca, bấm **Kiểm kê sản phẩm đang chọn...**, nhập số đếm ít hơn sổ 3 cái → app kết luận "Thực tế ít hơn sổ 3 cái: thất thoát". Bấm Cập nhật khi chưa chọn lý do → bị chặn. Chọn **Mất** → cập nhật.
- Tab **Nhật ký kho**: thấy đủ cả hai chiều lệch (Bán ngoài sổ kèm mã hóa đơn, Kiểm kê: thất thoát kèm lý do), ai làm, lúc nào. Thẻ đầu trang là **tổng thất thoát** trong kỳ; có sẵn vài dòng mẫu.

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
10. **Admin và Thu ngân có quyền gì?** Thu ngân bán hàng và tự đổi mật khẩu. Admin bán hàng, quản lý kho (nhập kho, kiểm kê), xem hóa đơn và doanh thu, xem nhật ký kho và thất thoát, quản lý tài khoản (tạo, khóa/mở khóa, đặt lại mật khẩu). Có chốt chặn: không thể tự khóa mình, không thể khóa Admin cuối cùng, nên hệ thống không bao giờ bị khóa hết quyền quản trị.
11. **Hạn chế hiện tại?** Chỉ chạy một máy; phân quyền ở giao diện; CSDL không mã hóa; chưa có in hóa đơn, phiếu nhập gồm nhiều sản phẩm và giá nhập, trả hàng, khuyến mãi.
12. **Triển khai thực tế cần cải tiến gì?** CSDL máy chủ hoặc API cho nhiều quầy, phân quyền ở server, in hóa đơn, phiếu nhập kho và kiểm kê, sao lưu tự động, quản lý ca làm việc.

13. **Kho báo hết nhưng khách vẫn cầm được hàng thì sao?** Vẫn bán được. Thu ngân xác nhận thì hóa đơn ghi đầy đủ, không mất doanh thu; sản phẩm hiện "bán ngoài sổ: N" màu đỏ để Admin biết. Bên trong, phần mềm ghi tồn kho là -N: đây là số sổ sách chứ không phải số hàng trên kệ. Giữ số này để khi kiểm kê tính đúng quy mô lô chưa ghi: đã bán 1 ngoài sổ, đếm được 2 thì lô đó là 3 cái.
    **Khi nhà cung cấp giao lô mới thì sao?** App không cho nhập ngay nếu còn hàng bán ngoài sổ, vì số đó thuộc lô trước. Phải kiểm kê chốt lô cũ trước (đếm trước khi xếp hàng mới lên kệ), rồi mới nhập lô mới theo hóa đơn. Nhờ vậy số nhập trong nhật ký khớp đúng tờ hóa đơn của nhà cung cấp: lô cũ +3, lô mới +10, tồn 12 đúng thực tế.
14. **Kho báo còn nhưng không tìm thấy hàng thì sao?** Đó là thất thoát. Admin kiểm kê, nhập số đếm thực tế và bắt buộc chọn lý do (Mất, Hỏng, Hết hạn, Đếm sai trước đó). Mọi lần kiểm kê đều vào **Nhật ký kho** với người làm, thời gian, số trước và sau.
15. **Thất thoát tháng này bao nhiêu, xem ở đâu?** Tab Nhật ký kho, chọn khoảng ngày. Mất, Hỏng, Hết hạn được cộng vào thất thoát; "Đếm sai trước đó" thì không, vì hàng không thật sự mất. Con số tính **theo giá bán** vì app chưa có giá nhập, nên phản ánh doanh thu bị mất chứ không phải tiền vốn.
16. **Có bán nhầm khi hết hàng thật không? Nhân viên có che giấu mất hàng được không?** Mỗi sản phẩm phải được xác nhận riêng và hộp thoại mặc định chọn Không; BLL chỉ cho bán ngoài sổ đúng sản phẩm đã xác nhận. Trong app không có chức năng sửa hay xóa nhật ký, nên mọi thay đổi đều có tên người làm. Giới hạn: người mở được trực tiếp file database vẫn sửa được, vì database không mã hóa; và lý do "Đếm sai trước đó" do Admin tự chọn, nên chủ cửa hàng cần xem nhật ký định kỳ.

### Câu hỏi kỹ thuật có thể gặp
- **Làm sao trừ kho an toàn?** `OrderService.CreateOrder` kiểm tra tồn kho, trừ kho và lưu hóa đơn trong **một transaction**; lỗi ở bất kỳ bước nào thì hủy toàn bộ, kho không bị trừ.
- **Giá có bị sửa từ giao diện được không?** Không. Khi thanh toán, giá được đọc lại từ CSDL, không dùng giá giao diện gửi lên.
- **Mật khẩu lưu thế nào?** Băm PBKDF2-SHA256, 100.000 vòng, có salt ngẫu nhiên; không lưu mật khẩu gốc.
- **Có bị SQL Injection không?** Không: mọi truy vấn qua Entity Framework LINQ, tham số hóa tự động, không có SQL nối chuỗi.
- **Xóa sản phẩm đã bán thì lịch sử ra sao?** Chỉ ngừng kinh doanh (xóa mềm), hóa đơn cũ vẫn hiển thị đúng sản phẩm.
