# Giá vốn, công nợ và xử lý thất thoát — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Biến CHTLManager thành phần mềm một cửa hàng thật dùng được: biết lãi bao nhiêu, nợ nhà cung cấp bao nhiêu, ai chịu trách nhiệm khoản thất thoát; mọi con số truy được về chứng từ; nâng cấp không mất dữ liệu.

**Architecture:** Giữ 3 tầng WPF → BLL → DAL và code-behind. Thêm chứng từ `PurchaseReceipt` (phiếu nhập), `StockDiscrepancy` (chênh lệch kiểm kê), `SupplierPayment`, `CompensationPayment`. `StockMovement` vẫn là nhật ký mọi thay đổi tồn kho ngoài bán thường. Sai sót được sửa bằng trạng thái hủy và dòng đảo ngược, không xóa dữ liệu.

**Tech Stack:** C#, .NET 8, WPF, EF Core 8.0.11 + SQLite, EF Migrations, xUnit (mới).

**Spec:** Chưa có file spec riêng. Mục "Quyết định thiết kế" dưới đây đóng vai trò spec; **người dùng duyệt plan này tức là duyệt các quyết định đó**.

## Global Constraints

- Tiêu chuẩn: thiết kế cho người dùng thật, không có đường tắt chỉ dành cho demo. Không dùng "xóa database" làm cách nâng cấp.
- Quy trình repo: mỗi Task xong thì build + test + báo cáo, **chờ người dùng duyệt rồi mới commit**, mỗi Task một commit. Push là quyết định riêng. Bước "Commit" trong plan luôn hiểu theo quy tắc này.
- Giao diện và thông báo bằng tiếng Việt. Người dùng không bao giờ thấy số âm: dùng `Product.StockDisplay`.
- Lỗi nghiệp vụ ném `BusinessException`. SQLite không `Sum`/`OrderBy` được `decimal`: tổng hợp trong bộ nhớ.
- Nghiệp vụ mới đặt ở BLL, có test. BLL vẫn chưa kiểm tra vai trò (hạn chế đã biết, ngoài phạm vi).
- Mỗi lần đổi tồn kho ngoài bán thường phải ghi `StockMovement` trong cùng `SaveChanges`.
- Mỗi Task kết thúc với app chạy được, có thể demo.

## Quyết định thiết kế

**Giá vốn:** bình quân gia quyền tức thời, `Product.AverageCost`, cập nhật khi phiếu nhập có giá. `Product.LastCost` là giá nhập gần nhất. Dòng hóa đơn bán lưu `OrderDetail.UnitCost` tại lúc bán. Bán ngoài sổ hoặc chưa có giá vốn thì đánh dấu `CostIsEstimated = true`, lấy `AverageCost`, nếu bằng 0 thì `LastCost`, nếu vẫn 0 thì 0. Không sửa lại giá vốn của hóa đơn đã bán; báo cáo hiện số dòng tạm tính để người xem biết độ tin cậy.

**Phiếu nhập:** một nhà cung cấp + một số hóa đơn, nhiều dòng sản phẩm. Mỗi dòng có SL theo hóa đơn, SL thực nhận, giá nhập. Công nợ tính theo **thực nhận × giá nhập**. Nhận thiếu so với hóa đơn chỉ hiện trên phiếu, không phải thất thoát của cửa hàng. Không trùng (nhà cung cấp + số hóa đơn) trừ phiếu đã hủy, so sánh sau khi bỏ khoảng trắng và không phân biệt hoa thường.

**Thu ngân nhận hàng:** tạo phiếu `Pending`, không nhập giá, tồn kho tăng ngay để bán được. Admin đối chiếu rồi nhập giá để `Confirmed` (lúc này mới tính giá vốn và công nợ) hoặc `Cancelled` (trừ lại kho). Admin tạo phiếu thì `Confirmed` ngay. Sản phẩm chưa có trong danh mục: để riêng chờ Admin tạo.

**Kiểm kê:** bước đếm chỉ ghi số đếm được; tồn kho đổi ngay; chênh lệch tạo `StockDiscrepancy` trạng thái `Pending`. Thu ngân được đếm (để nhận hàng khi còn bán ngoài sổ), chỉ Admin được xử lý. Bỏ chọn lý do ở cửa sổ kiểm kê.

**Xử lý chênh lệch (Admin):**

| Chiều | Cách xử lý | Tác động |
|---|---|---|
| Thiếu | `NormalLoss` hao hụt / hỏng / hết hạn | Chi phí thất thoát theo giá vốn |
| Thiếu | `Compensation` có người chịu trách nhiệm | Như trên, thêm số phải thu bồi thường và các lần thu |
| Thiếu | `Miscount` đếm sai | Không vào chi phí nhưng **bắt buộc ghi chú** và hiện thành thẻ riêng trong báo cáo |
| Thừa | `UnrecordedPurchase` lô chưa ghi | Tạo phiếu nhập `Confirmed` liên kết, sinh công nợ, **không cộng kho lần hai** |
| Thừa | `OtherIncome` không rõ nguồn | Thu nhập khác theo giá vốn |
| Thừa | `Miscount` đếm sai | Như dòng Miscount trên |

Dòng xử lý lưu người xử lý; nếu trùng người kiểm kê thì hiện "Tự xử lý" trong danh sách. App chỉ theo dõi số phải thu bồi thường, **không tự trừ lương**.

**Báo cáo lợi nhuận:** Doanh thu − Giá vốn = **Lãi gộp**; − Chi phí thất thoát + Thu nhập khác + Bồi thường đã thu = **Lãi sau chênh lệch kho**. Không tính chi phí vận hành (lương, mặt bằng). Hóa đơn đã hủy không tính.

**Hủy hóa đơn bán:** chỉ Admin, hủy cả hóa đơn, bắt buộc lý do; hoàn kho qua `StockMovement` loại `SaleCancelled`, đưa hàng về theo giá vốn đã lưu trên dòng. Không có trả hàng từng phần.

**Giới hạn bán ngoài sổ:** tối đa `OrderService.MaxOutOfBookPerLine = 10` cái vượt tồn trên một dòng hóa đơn, tính bằng `SL − max(tồn, 0)`; quá mức thì thu ngân phải gọi Admin nhận hàng hoặc kiểm kê.

**Dữ liệu mẫu:** chỉ nạp khi chạy với tham số `--demo`. Cửa hàng thật lần đầu chỉ có tài khoản Admin, không có hàng hay hóa đơn giả.

**Ngoài phạm vi:** sổ kế toán, thuế, trả hàng cho nhà cung cấp, trả hàng từng phần, chi phí vận hành, kiểm tra vai trò ở BLL, bắt đổi mật khẩu mặc định, nhiều máy.

## Review Focus

Mỗi dòng có test trong Task sở hữu phần code đó.

1. **Mở bản mới trên database cũ đang có dữ liệu thật:** dữ liệu còn nguyên, có file sao lưu; nếu nâng cấp lỗi thì app báo rõ và dừng, không ghi đè. (Task 1)
2. **Admin từ chối phiếu sau khi hàng đã bán hết:** kho về trạng thái bán ngoài sổ, không lỗi, nhật ký đủ. (Task 3)
3. **Gõ lại số hóa đơn có khoảng trắng, khác hoa thường:** bị chặn trùng; phiếu đã hủy thì dùng lại được. (Task 3)
4. **Trả tiền nhà cung cấp hoặc thu bồi thường quá số còn lại, số 0, số âm:** bị chặn, số dư không đổi. (Task 5, 6)
5. **Hủy hóa đơn hai lần, hoặc hủy hóa đơn của sản phẩm đã ngừng bán:** lần hai bị chặn, không hoàn kho hai lần; hóa đơn đầu vẫn hoàn kho được. (Task 7)

## Bản đồ file

| File | Trách nhiệm |
|---|---|
| `CHTLManager.Tests/` (mới) | Test xUnit cho BLL, dùng database tạm |
| `CHTLManager.DAL/Migrations/` (mới) | EF Migrations |
| `CHTLManager.DAL/Entities/Purchasing.cs` (mới) | `Supplier`, `PurchaseReceipt`, `PurchaseReceiptLine`, `SupplierPayment` |
| `CHTLManager.DAL/Entities/StockDiscrepancy.cs` (mới) | `StockDiscrepancy`, `CompensationPayment`, các enum |
| `CHTLManager.BLL/DatabaseUpgrader.cs` (mới) | Sao lưu, nhận diện database cũ, chạy migration |
| `CHTLManager.BLL/CostCalculator.cs` (mới) | Công thức giá vốn, không phụ thuộc database |
| `CHTLManager.BLL/PurchaseService.cs` (mới) | Phiếu nhập, nhà cung cấp, công nợ |
| `CHTLManager.BLL/StockCountService.cs` (mới) | Kiểm kê và xử lý chênh lệch |
| `CHTLManager.BLL/Reports/ProfitReport.cs` (mới) | Số liệu lợi nhuận |
| `CHTLManager.WPF/Views/Purchase*.xaml` (mới) | Tab Nhập hàng & NCC, cửa sổ phiếu nhập |
| `CHTLManager.WPF/Views/Discrepancy*.xaml` (mới) | Tab Chênh lệch kho, cửa sổ xử lý |

---

### Task 1: Project test và EF Migrations an toàn cho database đang có

**Files:**
- Create: `CHTLManager.Tests/CHTLManager.Tests.csproj`, `CHTLManager.Tests/TestDb.cs`, `CHTLManager.Tests/DatabaseUpgraderTests.cs`, `CHTLManager.BLL/DatabaseUpgrader.cs`, `CHTLManager.DAL/AppDbContextFactory.cs`, `CHTLManager.DAL/Migrations/*`, `.config/dotnet-tools.json`
- Modify: `CHTLManager.slnx`, `CHTLManager.DAL/CHTLManager.DAL.csproj`, `CHTLManager.BLL/DbInitializer.cs`, `CHTLManager.WPF/App.xaml.cs`, `CLAUDE.md`, `README.md`, `docs/DEMO.md`

**Interfaces:**
- Produces: `TestDb : IDisposable` (`Path`, `CreateServices()`; gán `AppDbContext.DbPath` sang file tạm); `DatabaseUpgrader.Upgrade() : UpgradeResult { string? BackupPath; bool WasLegacy; bool IsNew }`; `DbInitializer.Initialize(bool seedDemoData)`.

- [ ] **Step 1: Tạo project test.** xUnit, `net8.0`, tham chiếu BLL, thêm vào `.slnx`, `[assembly: CollectionBehavior(DisableTestParallelization = true)]` vì `AppDbContext.DbPath` là static. Thêm `Microsoft.EntityFrameworkCore.Design` 8.0.11 vào DAL, tool `dotnet-ef` 8.0.11 qua manifest cục bộ, và `IDesignTimeDbContextFactory<AppDbContext>`.
- [ ] **Step 2: Tạo migration nền.** Run: `dotnet ef migrations add InitialCreate --project CHTLManager.DAL`. Expected: migration khớp schema hiện tại, gồm `StockMovements` có `Supplier`, `InvoiceNo`.
- [ ] **Step 3: Viết test thất bại.**

```csharp
[Fact] void NewFile_CreatesSchema_NoBackup()                       // IsNew == true, BackupPath == null
[Fact] void LegacyFile_KeepsData_MakesBackup_MarksBaseline()       // file tạo bằng EnsureCreated có 1 Order:
                                                                   // WasLegacy, Order còn, File.Exists(BackupPath), lịch sử có InitialCreate
[Fact] void LegacyFile_MissingStockMovementColumns_IsRepaired()    // bỏ Supplier/InvoiceNo trước khi nâng cấp
[Fact] void UpToDateFile_NoBackup_NoError()                        // chạy lần hai
[Fact] void BrokenFile_Throws_LeavesFileUnchanged()                // file rác: ném BusinessException, nội dung byte không đổi
```

- [ ] **Step 4: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: FAIL, chưa có `DatabaseUpgrader`.
- [ ] **Step 5: Implement `DatabaseUpgrader.Upgrade()`.** Thứ tự: file chưa có → `Migrate`. File có bảng `Users` nhưng không có `__EFMigrationsHistory` là database cũ: sao lưu, chạy `EnsureStockMovementsTable` hiện có để bù schema, tạo bảng lịch sử và ghi baseline `InitialCreate`, rồi `Migrate`. Có migration chờ chạy: sao lưu trước. Sao lưu bằng `VACUUM INTO` sang `chtlmanager.backup-yyyyMMdd-HHmmss.db`, giữ 5 bản gần nhất. Lỗi nào cũng bọc thành `BusinessException` nêu đường dẫn file sao lưu.
- [ ] **Step 6: Tách dữ liệu mẫu.** `Initialize(false)` chỉ tạo Admin; `Initialize(true)` tạo thêm thu ngân, sản phẩm, hóa đơn mẫu khi database mới. `App.OnStartup` gọi `DatabaseUpgrader.Upgrade()` rồi `Initialize(e.Args.Contains("--demo"))`, lỗi thì hiện thông báo và thoát. Sửa `docs/DEMO.md` dùng `--demo`.
- [ ] **Step 7: Run test và build.** Run: `dotnet test CHTLManager.Tests` rồi `dotnet build CHTLManager.slnx`. Expected: tất cả PASS, 0 cảnh báo.
- [ ] **Step 8: Kiểm tra thật.** Mở app với file database cũ thật tạo từ commit `0b729d8`; đăng nhập, bán một đơn, thấy dữ liệu cũ còn và có file sao lưu.
- [ ] **Step 9: Commit** `feat: migrations with backup and separate demo data`.

### Task 2: Vá các lỗ hổng đã tái hiện

**Files:**
- Modify: `CHTLManager.BLL/ProductService.cs`, `CHTLManager.BLL/OrderService.cs`, `CHTLManager.WPF/Views/ProductUserControl.xaml.cs`, `CHTLManager.WPF/Views/POSUserControl.xaml.cs`
- Test: `CHTLManager.Tests/ProductLifecycleTests.cs`, `CHTLManager.Tests/OutOfBookLimitTests.cs`

**Interfaces:**
- Produces: `OrderService.MaxOutOfBookPerLine = 10` (public const).

- [ ] **Step 1: Viết test thất bại.**

```csharp
[Fact] void Delete_WithNonZeroStock_Blocked()               // kể cả tồn âm; thông báo yêu cầu kiểm kê về 0
[Fact] void Delete_ZeroStockWithHistory_SoftDeletes()
[Fact] void Add_ReactivatedProduct_IgnoresFormStock()       // không đổi Stock, không có dòng Initial mới
[Fact] void CreateOrder_OutOfBookOver10_Blocked()           // tồn 0, xác nhận, SL 11: BusinessException, kho không đổi
[Fact] void CreateOrder_OutOfBookExactly10_Allowed()
[Fact] void CreateOrder_Stock5_Sell15_Confirmed_Allowed()   // vượt đúng 10
```

- [ ] **Step 2: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: FAIL ở các test trên.
- [ ] **Step 3: Sửa `ProductService.Delete`, `Add`, `OrderService.CreateOrder`.** Giao diện: thông báo khi kinh doanh lại rằng tồn kho giữ nguyên; giỏ hàng báo giới hạn trước khi thanh toán (`POSUserControl.ConfirmOversell` và các điểm tăng số lượng).
- [ ] **Step 4: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: PASS.
- [ ] **Step 5: Commit** `fix: close stock-control loopholes`.

### Task 3: Nhà cung cấp và phiếu nhập

**Files:**
- Create: `CHTLManager.DAL/Entities/Purchasing.cs`, `CHTLManager.BLL/PurchaseService.cs`, `CHTLManager.BLL/Interfaces/IPurchaseService.cs`, `CHTLManager.WPF/Views/PurchaseUserControl.xaml(.cs)`, `CHTLManager.WPF/Views/PurchaseReceiptWindow.xaml(.cs)`, migration `AddPurchasing`
- Modify: `CHTLManager.DAL/AppDbContext.cs`, `CHTLManager.BLL/ProductService.cs` (bỏ `ImportStock`), `CHTLManager.WPF/AppServices.cs`, `CHTLManager.WPF/Views/MainWindow.xaml(.cs)`, `CHTLManager.WPF/Views/POSUserControl.xaml(.cs)`, `CHTLManager.WPF/Views/ProductUserControl.xaml(.cs)`; xóa `ImportStockWindow`
- Test: `CHTLManager.Tests/PurchaseServiceTests.cs`

**Interfaces:**
- Consumes: `TestDb`; quy tắc "kiểm kê trước khi nhập khi `Stock < 0`".
- Produces:
  - `enum ReceiptStatus { Pending = 1, Confirmed = 2, Cancelled = 3 }`
  - `Supplier { Id, Name, Phone?, IsActive }`, tên duy nhất không phân biệt hoa thường
  - `PurchaseReceipt { Id, Code ("PNyyyyMMdd-NNNN"), SupplierId, InvoiceNo, InvoiceKey, ReceivedAt, CreatedByUserId, ConfirmedByUserId?, ConfirmedAt?, Status, CancelReason?, StockDiscrepancyId?, Lines; [NotMapped] decimal Total }`; index duy nhất `(SupplierId, InvoiceKey)` lọc `Status <> 'Cancelled'`
  - `PurchaseReceiptLine { Id, PurchaseReceiptId, ProductId, InvoicedQuantity, ReceivedQuantity, UnitCost?; [NotMapped] LineTotal, ShortQuantity }`
  - `record ReceiptLineInput(int ProductId, int InvoicedQuantity, int ReceivedQuantity, decimal? UnitCost)`
  - `IPurchaseService`: `Supplier SaveSupplier(Supplier s)`, `List<Supplier> GetSuppliers()`, `PurchaseReceipt CreateReceipt(int supplierId, string invoiceNo, IReadOnlyList<ReceiptLineInput> lines, int userId, bool asAdmin)`, `PurchaseReceipt ConfirmReceipt(int receiptId, IReadOnlyDictionary<int, decimal> unitCostByLineId, int adminId)`, `void CancelReceipt(int receiptId, string reason, int adminId)`, `List<PurchaseReceipt> GetReceipts(DateTime from, DateTime to, ReceiptStatus? status)`, `int CountPendingReceipts()`
  - `StockMovementType.ReceiptIn`, `StockMovementType.ReceiptCancelled`; `StockMovement.PurchaseReceiptId?`

- [ ] **Step 1: Viết test thất bại.**

```csharp
[Fact] void AdminReceipt_Confirmed_AddsReceivedQuantity_LogsMovementPerLine()
[Fact] void CashierReceipt_Pending_AddsStockNow_NoCostsRequired()
[Fact] void Receipt_RefusedWhenAnyProductHasNegativeStock_NothingSaved()
[Fact] void AdminReceipt_MissingCost_Rejected()
[Fact] void ReceivedLessThanInvoiced_ShowsShort_StockUsesReceived()     // HĐ 10, nhận 9: tồn +9, ShortQuantity 1
[Fact] void ReceivedMoreThanInvoiced_Allowed_ShortIsNegative()
[Fact] void DuplicateInvoice_SpacesAndCase_Blocked()                    // " abc-1 " và "ABC-1" (Review Focus 3)
[Fact] void CancelledReceipt_InvoiceCanBeReused()                       // (Review Focus 3)
[Fact] void ConfirmPending_RequiresCostForEveryLine()
[Fact] void CancelPending_AfterGoodsSold_StockGoesOutOfBook_Logged()    // (Review Focus 2)
[Fact] void CancelConfirmedOrCancelledTwice_SecondBlocked()
```

- [ ] **Step 2: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: FAIL.
- [ ] **Step 3: Thêm entity và migration.** Run: `dotnet ef migrations add AddPurchasing --project CHTLManager.DAL`. Dữ liệu nhập cũ trong `StockMovements` (`Type = Import`) giữ nguyên làm lịch sử, không chuyển thành phiếu; dòng có `Supplier` được tạo bản ghi `Supplier` tương ứng.
- [ ] **Step 4: Implement `PurchaseService`.** Sinh mã theo cùng cách `OrderService` (đếm trong ngày). Bỏ `ProductService.ImportStock`; giá vốn thêm ở Task 4.
- [ ] **Step 5: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: PASS.
- [ ] **Step 6: Làm giao diện.** Tab "Nhập hàng & NCC" (chỉ Admin): lọc ngày và trạng thái, danh sách phiếu, chi tiết dòng, Xác nhận/Hủy, thêm nhà cung cấp. `PurchaseReceiptWindow`: chọn nhà cung cấp, số hóa đơn, thêm nhiều dòng bằng mã vạch; cột giá chỉ hiện với Admin. Tab Bán hàng có nút "Nhận hàng" mở cửa sổ này dưới quyền thu ngân. Sản phẩm tồn âm: Admin được mở kiểm kê trước như hiện tại; thu ngân tạm thời được báo cần Admin kiểm kê (Task 6 sẽ cho thu ngân tự đếm). Khi Admin đăng nhập, header hiện "N phiếu chờ xác nhận" (bấm để mở tab).
- [ ] **Step 7: Chạy thử UI** với thu ngân và Admin: nhận hàng → bán → Admin xác nhận; và nhánh từ chối. Không có hộp thoại treo hay crash.
- [ ] **Step 8: Commit** `feat: suppliers and multi-line purchase receipts`.

### Task 4: Giá vốn và lợi nhuận

**Files:**
- Create: `CHTLManager.BLL/CostCalculator.cs`, `CHTLManager.BLL/Reports/ProfitReport.cs`, migration `AddCosting`
- Modify: `CHTLManager.DAL/Entities/Product.cs`, `OrderDetail.cs`, `CHTLManager.BLL/PurchaseService.cs`, `OrderService.cs`, `ProductService.cs`, `CHTLManager.WPF/Views/OrderHistoryUserControl.xaml(.cs)`, `ProductUserControl.xaml(.cs)`
- Test: `CHTLManager.Tests/CostCalculatorTests.cs`, `CHTLManager.Tests/ProfitTests.cs`

**Interfaces:**
- Consumes: `IPurchaseService.ConfirmReceipt`, `CreateReceipt`.
- Produces:
  - `Product.AverageCost`, `Product.LastCost` (`decimal(18,2)`); `OrderDetail.UnitCost`, `OrderDetail.CostIsEstimated`, `[NotMapped] LineCost`
  - `CostCalculator.AddToAverage(int stockBefore, decimal averageBefore, int quantity, decimal unitCost) : decimal`; `CostCalculator.RemoveFromAverage(int stockBefore, decimal averageBefore, int quantity, decimal unitCost) : decimal`; làm tròn 2 chữ số, `MidpointRounding.AwayFromZero`
  - `IOrderService.GetProfitReport(DateTime from, DateTime to) : ProfitReport { Revenue, CostOfGoods, GrossProfit, EstimatedLineCount, Products: List<ProductProfit> }`
  - `IProductService.SetOpeningCost(int productId, decimal cost, int adminId)`; `Add(Product product, decimal openingCost, int userId)`

- [ ] **Step 1: Viết test thất bại.**

```csharp
[Theory] // tồn, giá TB, SL, giá nhập, kỳ vọng
[InlineData(10, 100, 10, 120, 110)]
[InlineData(0, 100, 5, 120, 120)]     // hết hàng: lấy giá nhập mới
[InlineData(-1, 100, 5, 120, 120)]    // tồn âm không được kéo công thức sai
[InlineData(3, 0, 3, 100, 50)]        // giá cũ 0 vẫn đúng công thức
void AddToAverage_Cases(...)
[Fact] void RemoveFromAverage_RestoresOriginal()              // 20 cái TB 110, bỏ 10 giá 120 → 100
[Fact] void RemoveFromAverage_AllStockRemoved_KeepsAverage()  // không chia cho 0
[Fact] void ConfirmReceipt_UpdatesAverageAndLastCost_PendingDoesNot()
[Fact] void ConfirmAfterSales_UsesStockExcludingThisReceipt()
[Fact] void CreateOrder_StoresUnitCost_NotAffectedByLaterImport()
[Fact] void OutOfBookSale_CostFlaggedEstimated_UsesLastCostWhenAverageZero()
[Fact] void ProfitReport_MatchesHandCalculation_ExcludesOtherDates()
[Fact] void ProfitReport_NoCost_CountsEstimatedLines()
```

- [ ] **Step 2: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: FAIL.
- [ ] **Step 3: Implement.** Migration `AddCosting`: hóa đơn cũ có `UnitCost = 0`, `CostIsEstimated = true`. `ConfirmReceipt` dùng `stockBefore = Stock − ReceivedQuantity` (kẹp về 0). `CancelReceipt` của phiếu đã `Confirmed` gọi `RemoveFromAverage`.
- [ ] **Step 4: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: PASS.
- [ ] **Step 5: Làm giao diện.** Tab Báo cáo: thêm thẻ Giá vốn, Lãi gộp; bảng lợi nhuận theo sản phẩm; cảnh báo "N dòng giá vốn tạm tính". Tab Kho: cột Giá vốn (chỉ Admin), bộ lọc "Chưa có giá vốn", nút "Khai báo giá vốn", ô giá vốn khi thêm sản phẩm có tồn. Thu ngân không thấy giá vốn ở đâu.
- [ ] **Step 6: Chạy thử UI** nhập hai lô khác giá, bán, đối chiếu lãi gộp với tính tay.
- [ ] **Step 7: Commit** `feat: moving average cost and gross profit`.

### Task 5: Công nợ nhà cung cấp

**Files:**
- Modify: `CHTLManager.DAL/Entities/Purchasing.cs`, `CHTLManager.BLL/PurchaseService.cs`, `Interfaces/IPurchaseService.cs`, `CHTLManager.WPF/Views/PurchaseUserControl.xaml(.cs)`; migration `AddSupplierPayments`
- Create: `CHTLManager.WPF/Views/SupplierPaymentWindow.xaml(.cs)`
- Test: `CHTLManager.Tests/SupplierDebtTests.cs`

**Interfaces:**
- Consumes: `PurchaseReceipt.Total`.
- Produces: `SupplierPayment { Id, SupplierId, Amount, PaidAt, UserId, Note? }`; `SupplierBalance { Supplier, TotalPurchased, TotalPaid, Balance }`; `IPurchaseService.GetSupplierBalances() : List<SupplierBalance>`; `IPurchaseService.PaySupplier(int supplierId, decimal amount, string? note, int adminId) : SupplierPayment`; `IPurchaseService.GetSupplierHistory(int supplierId)`.

- [ ] **Step 1: Viết test thất bại.**

```csharp
[Fact] void ConfirmedReceipt_IncreasesDebt_ByReceivedTimesCost()   // HĐ 10, nhận 9, giá 100 → nợ 900
[Fact] void PendingAndCancelledReceipts_NotInDebt()
[Fact] void Payment_ReducesBalance()
[Theory][InlineData(0)][InlineData(-1)] void Payment_NonPositive_Blocked(decimal amount)  // (Review Focus 4)
[Fact] void Payment_OverBalance_Blocked_BalanceUnchanged()         // (Review Focus 4)
[Fact] void CancelConfirmedReceipt_AfterFullPayment_BlockedUntilResolved()
```

- [ ] **Step 2: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: FAIL.
- [ ] **Step 3: Implement.** Công nợ tính theo nhà cung cấp, không phân bổ vào từng phiếu. Hủy phiếu mà làm số dư âm thì chặn và báo rõ số đã trả.
- [ ] **Step 4: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: PASS.
- [ ] **Step 5: Làm giao diện.** Trong tab Nhập hàng & NCC: bảng nhà cung cấp với tổng mua, đã trả, còn nợ; nút "Trả tiền"; lịch sử phiếu và lần trả.
- [ ] **Step 6: Chạy thử UI** tạo phiếu → trả một phần → trả quá số nợ bị chặn.
- [ ] **Step 7: Commit** `feat: supplier debt and payments`.

### Task 6: Kiểm kê hai bước, xử lý chênh lệch, người chịu trách nhiệm

**Files:**
- Create: `CHTLManager.DAL/Entities/StockDiscrepancy.cs`, `CHTLManager.BLL/StockCountService.cs`, `Interfaces/IStockCountService.cs`, `CHTLManager.WPF/Views/DiscrepancyUserControl.xaml(.cs)`, `DiscrepancyResolveWindow.xaml(.cs)`; migration `AddDiscrepancies`
- Modify: `CHTLManager.BLL/ProductService.cs` (bỏ `SetCountedStock`), `Reports/StockLogReport.cs`, `OrderService.GetProfitReport`, `CHTLManager.DAL/Entities/StockMovement.cs`, `CHTLManager.WPF/Views/StockCountWindow.xaml(.cs)`, `StockLogUserControl.xaml(.cs)`, `OrderHistoryUserControl.xaml(.cs)`, `MainWindow.xaml(.cs)`
- Test: `CHTLManager.Tests/StockCountTests.cs`

**Interfaces:**
- Consumes: `CostCalculator.AddToAverage`, `Product.AverageCost`, `PurchaseReceipt`.
- Produces:
  - `IPurchaseService.RecordReceiptForCountedStock(int supplierId, string invoiceNo, int productId, int quantity, decimal unitCost, int stockDiscrepancyId, int adminId) : PurchaseReceipt`: phiếu `Confirmed` sinh công nợ, **không đổi `Stock`**; cũng áp dụng kiểm tra trùng số hóa đơn. Không dùng `CreateReceipt` ở đây vì hàm đó cộng kho.
  - `enum DiscrepancyStatus { Pending = 1, Resolved = 2 }`; `enum DiscrepancyResolution { NormalLoss = 1, Compensation = 2, Miscount = 3, UnrecordedPurchase = 4, OtherIncome = 5 }`
  - `StockDiscrepancy { Id, ProductId, CountedByUserId, CountedAt, BookStock, CountedStock, Delta, UnitCost, Status, Resolution?, ResolvedByUserId?, ResolvedAt?, Note?, ResponsibleUserId?, ResponsibleName?, CompensationAmount?; [NotMapped] Value, IsSelfResolved }`
  - `CompensationPayment { Id, StockDiscrepancyId, Amount, PaidAt, UserId, Note? }`
  - `IStockCountService`: `StockDiscrepancy? Count(int productId, int countedStock, int userId)` (trả `null` khi khớp, vẫn ghi `StockMovement`); `void ResolveShortage(int id, DiscrepancyResolution resolution, string? note, int? responsibleUserId, string? responsibleName, decimal? compensationAmount, int adminId)`; `void ResolveSurplus(int id, DiscrepancyResolution resolution, string? note, int? supplierId, string? invoiceNo, decimal? unitCost, int adminId)`; `CompensationPayment CollectCompensation(int id, decimal amount, string? note, int adminId)`; `List<StockDiscrepancy> GetDiscrepancies(DateTime from, DateTime to, DiscrepancyStatus? status)`; `int CountPending()`
  - `ProfitReport` thêm: `LossExpense, OtherIncome, CompensationCollected, MiscountShortValue, MiscountSurplusValue, NetAfterStockDifferences`

- [ ] **Step 1: Viết test thất bại.**

```csharp
[Fact] void Count_ChangesStockNow_CreatesPendingDiscrepancy_ValuedAtAverageCost()
[Fact] void Count_Match_NoDiscrepancy_StillLogged()
[Fact] void Pending_NotInLossExpense()
[Fact] void Shortage_NormalLoss_AddsExpense()
[Fact] void Shortage_Compensation_RequiresResponsibleAndAmount()
[Fact] void Shortage_Miscount_RequiresNote_NotExpense_ShownInMiscountValue()   // đóng lỗ hổng "Đếm sai"
[Fact] void Surplus_UnrecordedPurchase_CreatesConfirmedReceipt_DebtUp_StockNotAddedAgain()
                                 // sổ -1, đếm 2: Delta +3, phiếu 3 cái, nợ 3×giá, tồn vẫn 2
[Fact] void Surplus_UnrecordedPurchase_UpdatesAverageFromOnHandIncrease()
[Fact] void Surplus_OtherIncome_AddsIncome()
[Fact] void WrongResolutionForDirection_Blocked()
[Fact] void ResolveTwice_Blocked()
[Fact] void Collect_OverAmount_Zero_Negative_Blocked()                          // (Review Focus 4)
[Fact] void SameCounterAndResolver_FlaggedSelfResolved()
```

- [ ] **Step 2: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: FAIL.
- [ ] **Step 3: Implement.** Migration `AddDiscrepancies`: mỗi dòng `StockMovement` kiểm kê cũ có `Delta ≠ 0` thành một `StockDiscrepancy` đã `Resolved`, theo ánh xạ `Lost/Damaged/Expired → NormalLoss`, `MiscountBefore → Miscount`, `UnrecordedImport → OtherIncome` kèm ghi chú "Dữ liệu cũ", giá vốn 0. `Reason` trên `StockMovement` giữ lại để đọc lịch sử nhưng không ghi mới. `StockLogReport` không còn tính thất thoát; số liệu lấy từ `StockDiscrepancy`.
- [ ] **Step 4: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: PASS.
- [ ] **Step 5: Làm giao diện.** `StockCountWindow` chỉ còn số đếm và kết luận. Thu ngân gặp sản phẩm tồn âm trong luồng Nhận hàng giờ tự đếm được, rồi nhận hàng tiếp. Tab "Chênh lệch kho" (chỉ Admin): lọc trạng thái, thẻ tổng; `DiscrepancyResolveWindow` chỉ hiện cách xử lý đúng chiều; "Thu bồi thường"; hiện "Tự xử lý". Header cộng số chênh lệch chờ xử lý vào nhắc việc. Báo cáo thêm các thẻ thất thoát, thu nhập khác, bồi thường, lãi sau chênh lệch.
- [ ] **Step 6: Chạy thử UI** đủ hai chiều, kịch bản "lô cũ 3, bán 1, còn 2, lô mới 10": tồn 12, hai phiếu riêng, công nợ đúng.
- [ ] **Step 7: Commit** `feat: two-step stock count with responsibility and compensation`.

### Task 7: Hủy hóa đơn bán

**Files:**
- Modify: `CHTLManager.DAL/Entities/Order.cs`, `StockMovement.cs`, `CHTLManager.BLL/OrderService.cs`, `Interfaces/IOrderService.cs`, `CHTLManager.WPF/Views/OrderHistoryUserControl.xaml(.cs)`; migration `AddOrderCancellation`
- Test: `CHTLManager.Tests/CancelOrderTests.cs`

**Interfaces:**
- Consumes: `CostCalculator.AddToAverage`, `OrderDetail.UnitCost`, `ProfitReport`.
- Produces: `Order.IsCancelled`, `CancelledAt?`, `CancelledByUserId?`, `CancelReason?`; `IOrderService.CancelOrder(int orderId, string reason, int adminId)`; `StockMovementType.SaleCancelled`.

- [ ] **Step 1: Viết test thất bại.**

```csharp
[Fact] void Cancel_RestoresStock_LogsMovement_KeepsOrderInHistory()
[Fact] void Cancel_ExcludedFromRevenueAndProfit()
[Fact] void Cancel_RequiresReason()
[Fact] void CancelTwice_Blocked_StockRestoredOnce()              // (Review Focus 5)
[Fact] void Cancel_OrderOfDiscontinuedProduct_Works()            // (Review Focus 5)
[Fact] void Cancel_OutOfBookOrder_ReturnsLedgerToBefore()
[Fact] void Cancel_AddsBackAtStoredCost()
[Fact] void NewOrderCode_AfterCancel_DoesNotCollide()
```

- [ ] **Step 2: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: FAIL.
- [ ] **Step 3: Implement `CancelOrder`** trong một transaction. Mã hóa đơn vẫn đếm cả hóa đơn đã hủy nên không trùng.
- [ ] **Step 4: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: PASS.
- [ ] **Step 5: Làm giao diện.** Tab Báo cáo: nút "Hủy hóa đơn" hỏi lý do và xác nhận; hóa đơn đã hủy hiện màu đỏ kèm lý do và người hủy.
- [ ] **Step 6: Commit** `feat: cancel sales with stock reversal`.

### Task 8: Rà soát toàn bộ, tài liệu, đóng gói

**Files:**
- Modify: `README.md`, `CLAUDE.md`, `docs/DEMO.md`, `docs/PLAN.md`, `CHTLManager.BLL/DbInitializer.cs` (dữ liệu demo có giá vốn, phiếu, công nợ, chênh lệch nhất quán)

- [ ] **Step 1: Viết test** `DemoSeed_Reconciles()`: với mọi sản phẩm, tồn hiện tại = ban đầu + nhập − bán + hủy ± kiểm kê; công nợ và lãi gộp khớp tính tay.
- [ ] **Step 2: Run test.** Run: `dotnet test CHTLManager.Tests`. Expected: FAIL, rồi sửa dữ liệu mẫu đến khi PASS.
- [ ] **Step 3: Chạy thử UI toàn bộ** theo kịch bản mới trên database `--demo` và trên database không có dữ liệu mẫu, kể cả sản phẩm chưa có giá vốn và cửa hàng lần đầu chưa có hàng.
- [ ] **Step 4: Nâng cấp thật:** mở bản mới trên database của commit `0b729d8` có dữ liệu; đủ dữ liệu, sao lưu có, báo cáo cũ không lỗi.
- [ ] **Step 5: Cập nhật tài liệu.** README, CLAUDE.md, kịch bản và câu hỏi bảo vệ (lợi nhuận, công nợ, nhận hàng, bồi thường, nâng cấp); nêu rõ giới hạn trong mục Ngoài phạm vi.
- [ ] **Step 6: Đóng gói.** Run lệnh publish trong README, thêm `--demo` qua shortcut hoặc tham số. Chạy file `.exe` từ thư mục trống. Expected: mở được cả hai chế độ.
- [ ] **Step 7: Commit** `docs: update guides and demo data for costing and debts`. Push là quyết định riêng của người dùng.
