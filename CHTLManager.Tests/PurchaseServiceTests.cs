using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public sealed class PurchaseFixture : IDisposable
{
    public TestDb Db { get; } = new TestDb().Initialized();
    public PurchaseService Purchases { get; } = new();
    public int AdminId { get; }
    public int CashierId { get; }
    public Supplier Supplier { get; }
    public Product Coca { get; }
    public Product Oreo { get; }

    public PurchaseFixture()
    {
        AdminId = Db.Users.Login("admin", "admin123")!.Id;
        CashierId = Db.Users.Create("tn", "123456", "Thu ngân", UserRole.Cashier).Id;
        Supplier = Purchases.SaveSupplier(new Supplier { Name = "NCC A" });
        Coca = Db.Products.Add(new Product { Barcode = "111", Name = "Coca", Price = 10000, Stock = 0 }, AdminId);
        Oreo = Db.Products.Add(new Product { Barcode = "222", Name = "Oreo", Price = 16000, Stock = 5 }, AdminId);
    }

    public int Stock(Product p) => Db.Products.GetById(p.Id)!.Stock;

    public PurchaseReceipt AdminReceipt(string invoice, int qty = 10, decimal cost = 7000)
        => Purchases.CreateReceipt(Supplier.Id, invoice, new[] { new ReceiptLineInput(Coca.Id, qty, qty, cost) }, AdminId, asAdmin: true);

    public PurchaseReceipt CashierReceipt(string invoice, int qty = 10)
        => Purchases.CreateReceipt(Supplier.Id, invoice, new[] { new ReceiptLineInput(Coca.Id, qty, qty, null) }, CashierId, asAdmin: false);

    public Order Sell(Product p, int qty, bool outOfBook = false) => Db.Orders.CreateOrder(
        new Order { CashierId = CashierId, CustomerPaid = 100_000_000 },
        new() { new OrderDetail { ProductId = p.Id, Quantity = qty } },
        outOfBook ? new HashSet<int> { p.Id } : null);

    public void Dispose() => Db.Dispose();
}

public class PurchaseServiceTests
{
    [Fact]
    public void SaveSupplier_SameNameDifferentCase_ReturnsExisting()
    {
        using var f = new PurchaseFixture();

        var again = f.Purchases.SaveSupplier(new Supplier { Name = "  ncc a " });

        Assert.Equal(f.Supplier.Id, again.Id);
        Assert.Single(f.Purchases.GetSuppliers());
    }

    [Fact]
    public void SaveSupplier_BlankName_Blocked()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Purchases.SaveSupplier(new Supplier { Name = "  " }));
    }

    [Fact]
    public void AdminReceipt_Confirmed_AddsReceivedQuantity_LogsMovementPerLine()
    {
        using var f = new PurchaseFixture();

        var r = f.Purchases.CreateReceipt(f.Supplier.Id, "HD1", new[]
        {
            new ReceiptLineInput(f.Coca.Id, 10, 10, 7000),
            new ReceiptLineInput(f.Oreo.Id, 4, 4, 12000)
        }, f.AdminId, asAdmin: true);

        Assert.Equal(ReceiptStatus.Confirmed, r.Status);
        Assert.StartsWith($"PN{DateTime.Now:yyyyMMdd}-", r.Code);
        Assert.Equal(10, f.Stock(f.Coca));
        Assert.Equal(9, f.Stock(f.Oreo));
        Assert.Equal(118000m, r.Total);

        var logged = f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements
            .Where(m => m.Type == StockMovementType.ReceiptIn).ToList();
        Assert.Equal(2, logged.Count);
        Assert.All(logged, m => Assert.Equal(r.Id, m.PurchaseReceiptId));
        Assert.Contains(logged, m => m.ProductId == f.Oreo.Id && m.BeforeStock == 5 && m.AfterStock == 9 && m.Detail == "NCC A, HĐ HD1");
    }

    [Fact]
    public void CashierReceipt_Pending_AddsStockNow_NoCostsRequired()
    {
        using var f = new PurchaseFixture();

        var r = f.CashierReceipt("HD1");

        Assert.Equal(ReceiptStatus.Pending, r.Status);
        Assert.Equal(10, f.Stock(f.Coca));
        Assert.Equal(1, f.Purchases.CountPendingReceipts());
        Assert.Null(r.ConfirmedByUserId);
    }

    [Fact]
    public void Receipt_RefusedWhenAnyProductHasNegativeStock_NothingSaved()
    {
        using var f = new PurchaseFixture();
        f.Sell(f.Coca, 1, outOfBook: true);

        var ex = Assert.Throws<BusinessException>(() => f.Purchases.CreateReceipt(f.Supplier.Id, "HD1", new[]
        {
            new ReceiptLineInput(f.Oreo.Id, 4, 4, 12000),
            new ReceiptLineInput(f.Coca.Id, 10, 10, 7000)
        }, f.AdminId, asAdmin: true));

        Assert.Contains("kiểm kê", ex.Message);
        Assert.Equal(5, f.Stock(f.Oreo));
        Assert.Empty(f.Purchases.GetReceipts(DateTime.Today, DateTime.Today, null));
    }

    [Fact]
    public void AdminReceipt_MissingCost_Rejected()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Purchases.CreateReceipt(f.Supplier.Id, "HD1",
            new[] { new ReceiptLineInput(f.Coca.Id, 10, 10, null) }, f.AdminId, asAdmin: true));
        Assert.Equal(0, f.Stock(f.Coca));
    }

    [Fact]
    public void AdminReceipt_NegativeCost_Rejected()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.AdminReceipt("HD1", cost: -1));
    }

    [Fact]
    public void AdminReceipt_ZeroCost_AllowedForFreeGoods()
    {
        using var f = new PurchaseFixture();
        Assert.Equal(0m, f.AdminReceipt("HD1", cost: 0).Total);
    }

    [Fact]
    public void ReceivedLessThanInvoiced_ShowsShort_StockUsesReceived()
    {
        using var f = new PurchaseFixture();

        var r = f.Purchases.CreateReceipt(f.Supplier.Id, "HD1",
            new[] { new ReceiptLineInput(f.Coca.Id, 10, 9, 100) }, f.AdminId, asAdmin: true);

        Assert.Equal(9, f.Stock(f.Coca));
        Assert.Equal(1, r.Lines[0].ShortQuantity);
        Assert.Equal(900m, r.Total);
    }

    [Fact]
    public void ReceivedMoreThanInvoiced_Allowed_ShortIsNegative()
    {
        using var f = new PurchaseFixture();

        var r = f.Purchases.CreateReceipt(f.Supplier.Id, "HD1",
            new[] { new ReceiptLineInput(f.Coca.Id, 10, 12, 100) }, f.AdminId, asAdmin: true);

        Assert.Equal(-2, r.Lines[0].ShortQuantity);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    public void InvalidQuantities_Rejected(int invoiced, int received)
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Purchases.CreateReceipt(f.Supplier.Id, "HD1",
            new[] { new ReceiptLineInput(f.Coca.Id, invoiced, received, 100) }, f.AdminId, asAdmin: true));
    }

    [Fact]
    public void NothingReceived_Rejected()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Purchases.CreateReceipt(f.Supplier.Id, "HD1",
            new[] { new ReceiptLineInput(f.Coca.Id, 5, 0, 100) }, f.AdminId, asAdmin: true));
    }

    [Fact]
    public void NoLines_Rejected()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Purchases.CreateReceipt(f.Supplier.Id, "HD1", Array.Empty<ReceiptLineInput>(), f.AdminId, true));
    }

    [Fact]
    public void BlankInvoice_Rejected()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.AdminReceipt("  "));
    }

    [Fact]
    public void DuplicateProductLine_Rejected()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Purchases.CreateReceipt(f.Supplier.Id, "HD1", new[]
        {
            new ReceiptLineInput(f.Coca.Id, 1, 1, 100), new ReceiptLineInput(f.Coca.Id, 1, 1, 100)
        }, f.AdminId, true));
    }

    [Fact]
    public void DuplicateInvoice_SpacesAndCase_Blocked()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt(" abc-1 ");

        var ex = Assert.Throws<BusinessException>(() => f.AdminReceipt("ABC-1"));

        Assert.Contains("ABC-1", ex.Message);
        Assert.Equal(10, f.Stock(f.Coca));
    }

    [Fact]
    public void SameInvoiceNumber_DifferentSupplier_Allowed()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1");
        var other = f.Purchases.SaveSupplier(new Supplier { Name = "NCC B" });

        f.Purchases.CreateReceipt(other.Id, "HD1", new[] { new ReceiptLineInput(f.Coca.Id, 1, 1, 100) }, f.AdminId, true);

        Assert.Equal(11, f.Stock(f.Coca));
    }

    [Fact]
    public void CancelledReceipt_InvoiceCanBeReused()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("HD1");
        f.Purchases.CancelReceipt(r.Id, "Nhập nhầm", f.AdminId);

        f.AdminReceipt("HD1", qty: 3);

        Assert.Equal(3, f.Stock(f.Coca));
    }

    [Fact]
    public void ConfirmPending_RequiresCostForEveryLine()
    {
        using var f = new PurchaseFixture();
        var r = f.Purchases.CreateReceipt(f.Supplier.Id, "HD1", new[]
        {
            new ReceiptLineInput(f.Coca.Id, 10, 10, null), new ReceiptLineInput(f.Oreo.Id, 2, 2, null)
        }, f.CashierId, asAdmin: false);

        Assert.Throws<BusinessException>(() => f.Purchases.ConfirmReceipt(r.Id,
            new Dictionary<int, decimal> { [r.Lines[0].Id] = 100 }, f.AdminId));

        var confirmed = f.Purchases.ConfirmReceipt(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 100m), f.AdminId);
        Assert.Equal(ReceiptStatus.Confirmed, confirmed.Status);
        Assert.Equal(f.AdminId, confirmed.ConfirmedByUserId);
        Assert.Equal(1200m, confirmed.Total);
        Assert.Equal(10, f.Stock(f.Coca));
        Assert.Equal(0, f.Purchases.CountPendingReceipts());
    }

    [Fact]
    public void Confirm_NotPending_Blocked()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("HD1");
        Assert.Throws<BusinessException>(() => f.Purchases.ConfirmReceipt(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 100m), f.AdminId));
    }

    [Fact]
    public void CancelPending_AfterGoodsSold_StockGoesOutOfBook_Logged()
    {
        using var f = new PurchaseFixture();
        var r = f.CashierReceipt("HD1");
        f.Sell(f.Coca, 8);

        f.Purchases.CancelReceipt(r.Id, "Hàng sai", f.AdminId);

        var product = f.Db.Products.GetById(f.Coca.Id)!;
        Assert.Equal(-8, product.Stock);
        Assert.Equal("0 (bán ngoài sổ: 8)", product.StockDisplay);
        var m = f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements[0];
        Assert.Equal(StockMovementType.ReceiptCancelled, m.Type);
        Assert.Equal(-10, m.Delta);
        Assert.Equal(f.AdminId, m.UserId);
    }

    [Fact]
    public void Cancel_RequiresReason()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("HD1");
        Assert.Throws<BusinessException>(() => f.Purchases.CancelReceipt(r.Id, " ", f.AdminId));
        Assert.Equal(10, f.Stock(f.Coca));
    }

    [Fact]
    public void CancelTwice_SecondBlocked_StockReversedOnce()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("HD1");
        f.Purchases.CancelReceipt(r.Id, "Nhầm", f.AdminId);

        Assert.Throws<BusinessException>(() => f.Purchases.CancelReceipt(r.Id, "Lại", f.AdminId));

        Assert.Equal(0, f.Stock(f.Coca));
    }

    [Fact]
    public void GetReceipts_FiltersDateAndStatus_IncludesDetails()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1");
        f.CashierReceipt("HD2");

        Assert.Equal(2, f.Purchases.GetReceipts(DateTime.Today, DateTime.Today, null).Count);
        var pending = Assert.Single(f.Purchases.GetReceipts(DateTime.Today, DateTime.Today, ReceiptStatus.Pending));
        Assert.Equal("NCC A", pending.Supplier!.Name);
        Assert.Equal("Coca", pending.Lines[0].Product!.Name);
        Assert.Empty(f.Purchases.GetReceipts(DateTime.Today.AddDays(-5), DateTime.Today.AddDays(-4), null));
    }

    [Fact]
    public void ReceiptCodes_Increment()
    {
        using var f = new PurchaseFixture();
        Assert.EndsWith("-0001", f.AdminReceipt("HD1").Code);
        Assert.EndsWith("-0002", f.AdminReceipt("HD2").Code);
    }

    [Fact]
    public void LegacyImports_BecomeSuppliers()
    {
        using var db = new TestDb();
        db.CreateLegacyFile();
        db.ExecuteSql("""
            INSERT INTO Products (Id, Barcode, Name, Category, Price, Stock, IsActive) VALUES (1, 'x', 'SP', '', 1000, 10, 1);
            INSERT INTO StockMovements (ProductId, UserId, CreatedAt, Type, BeforeStock, AfterStock, Delta, Reason, UnitPrice, Supplier, InvoiceNo) VALUES
              (1, 1, '2026-01-01', 'Import', 0, 5, 5, 'None', 1000, 'NPP Coca', '01'),
              (1, 1, '2026-01-02', 'Import', 5, 10, 5, 'None', 1000, 'npp coca ', '02');
            """);

        DatabaseUpgrader.Upgrade();

        var supplier = Assert.Single(new PurchaseService().GetSuppliers());
        Assert.Equal("npp coca", supplier.NameKey);
    }
}
