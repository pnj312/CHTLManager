# CHTLManager

Ứng dụng quản lý cửa hàng tiện lợi viết bằng WPF và .NET 10.

## Giới thiệu

Đây là bài tập lớn môn Công nghệ .NET, gồm giao diện bán hàng, quản lý sản phẩm và lưu hóa đơn vào SQL Server LocalDB.

## Chức năng đã triển khai

- **Đăng nhập:** xác thực bằng tên đăng nhập/mật khẩu; hỗ trợ đăng xuất để đăng nhập lại.
- **Phân quyền giao diện:** Admin dùng được tab Bán hàng và Quản lý Kho; Staff chỉ thấy tab Bán hàng.
- **Quản lý sản phẩm (Admin):** xem danh sách, tìm theo tên/mã vạch, thêm, sửa, xóa; kiểm tra dữ liệu nhập và quản lý số lượng tồn.
- **POS:** xem và tìm sản phẩm theo tên/mã vạch; nhấn Enter để tra cứu, mã vạch khớp chính xác được thêm vào giỏ. Có thể nhấp đúp sản phẩm, tăng/giảm số lượng, bỏ sản phẩm hoặc hủy giỏ; số lượng trong giỏ bị giới hạn theo tồn kho và tổng tiền được tính tạm thời.
- **Thanh toán:** nhập tiền khách đưa, kiểm tra số tiền và tồn kho, lưu `Order` cùng `OrderDetail`, tính tổng tiền/tiền thừa và giảm tồn kho trong một transaction.

## Kiến trúc

```text
CHTLManager.WPF (giao diện)
        ↓
CHTLManager.BLL (nghiệp vụ)
        ↓
CHTLManager.DAL (dữ liệu, EF Core)
        ↓
SQL Server Express LocalDB
```

WPF gọi các service ở BLL để thao tác dữ liệu, không truy vấn `AppDbContext` trực tiếp. WPF có tham chiếu DAL để sử dụng các entity hiện có.

## Cấu trúc project

```text
CHTLManager.slnx
├── CHTLManager.WPF/
│   ├── Views/             # LoginWindow, MainWindow, ProductUserControl, POSUserControl
│   ├── Models/            # CartItem
│   └── Resources/         # Styles.xaml
├── CHTLManager.BLL/
│   └── Services/          # UserService, ProductService, OrderService
└── CHTLManager.DAL/
    ├── Entities/          # User, Product, Order, OrderDetail
    ├── Migrations/        # InitialCreate
    └── AppDbContext.cs
```

## Công nghệ và yêu cầu

- C#, WPF: `CHTLManager.WPF` dùng `net10.0-windows`; BLL và DAL dùng `net10.0`.
- Entity Framework Core SQL Server và Design: `10.0.12` (trong DAL).
- Material Design for WPF (`MaterialDesignThemes`): `5.3.2`.
- Máy chạy **Windows**, có **.NET 10 SDK** và **SQL Server Express LocalDB** với instance `MSSQLLocalDB`.
- Cần công cụ dòng lệnh `dotnet-ef` 10.x để áp dụng migration. Visual Studio hỗ trợ .NET 10 là tùy chọn, không bắt buộc.

## Cài đặt và chạy

Trong PowerShell, clone repository từ thư mục bạn muốn lưu project, rồi restore và build tại thư mục gốc vừa tạo:

```powershell
git clone https://github.com/pnj312/CHTLManager.git
cd CHTLManager
dotnet restore .\CHTLManager.slnx
dotnet build .\CHTLManager.slnx
```

Kiểm tra LocalDB:

```powershell
sqllocaldb info
sqllocaldb info MSSQLLocalDB
```

Nếu `MSSQLLocalDB` chưa chạy, khởi động và kiểm tra lại:

```powershell
sqllocaldb start MSSQLLocalDB
sqllocaldb info MSSQLLocalDB
```

Nếu chưa có lệnh `sqllocaldb`, cần cài SQL Server Express LocalDB. Nếu đã cài nhưng thiếu instance, tạo bằng `sqllocaldb create MSSQLLocalDB` rồi khởi động. Connection string hiện tại trong `AppDbContext` là:

```text
Server=(localdb)\MSSQLLocalDB;Database=CHTLManagerDB;Trusted_Connection=True;TrustServerCertificate=True;
```

Nếu máy chưa có `dotnet ef`, cài công cụ CLI tương ứng với EF Core của project (không thêm package vào project):

```powershell
dotnet tool install --global dotnet-ef --version 10.0.12
```

Áp dụng migration **`InitialCreate` đã có sẵn** để tạo database và dữ liệu mẫu; không cần tạo migration mới:

```powershell
dotnet ef database update --project .\CHTLManager.DAL\CHTLManager.DAL.csproj --startup-project .\CHTLManager.DAL\CHTLManager.DAL.csproj --context AppDbContext
```

DAL được dùng làm startup project cho lệnh EF vì package `Microsoft.EntityFrameworkCore.Design` nằm ở DAL. Chạy ứng dụng từ WPF:

```powershell
dotnet run --project .\CHTLManager.WPF\CHTLManager.WPF.csproj
```

Ứng dụng mở tại `LoginWindow`. Nếu dùng Visual Studio, mở `CHTLManager.slnx`, chọn `CHTLManager.WPF` làm Startup Project rồi nhấn F5 hoặc Ctrl+F5.

## Tài khoản và dữ liệu demo

Migration `InitialCreate` tạo bốn bảng nghiệp vụ: `Users`, `Products`, `Orders`, `OrderDetails`; đồng thời seed 5 sản phẩm mẫu và hai tài khoản:

| Vai trò | Tên đăng nhập | Mật khẩu |
| --- | --- | --- |
| Admin | `admin` | `123456` |
| Staff | `staff` | `123456` |

Các tài khoản này chỉ phục vụ demo/bài tập.

## Lưu ý

- Mật khẩu demo hiện được lưu dạng văn bản thuần; không phải cơ chế bảo mật cho môi trường production.
- Connection string dùng LocalDB cho máy phát triển/học tập; ứng dụng không tự áp dụng migration khi khởi động.
- Quyền Admin/Staff hiện chủ yếu được kiểm soát ở giao diện, chưa có hệ thống phân quyền backend độc lập.
