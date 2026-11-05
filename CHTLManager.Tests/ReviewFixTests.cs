using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

/// <summary>Các tình huống do review toàn nhánh tìm ra.</summary>
public class ReviewFixTests
{
    private static StockCountService Counts => new();

    [Fact]
    public void FirstReceipt_OnProductWithStockButNoCost_DoesNotDiluteAverage()
    {
        using var f = new PurchaseFixture(); // Oreo tồn 5, chưa có giá vốn

        f.Purchases.CreateReceipt(f.Supplier.Id, "A", new[] { new ReceiptLineInput(f.Oreo.Id, 10, 10, 20000) }, f.AdminId, true);

        Assert.Equal(20000m, f.Db.Products.GetById(f.Oreo.Id)!.AverageCost);
    }

    [Fact]
    public void ConfirmReceipt_OnProductWithStockButNoCost_DoesNotDiluteAverage()
    {
        using var f = new PurchaseFixture();
        var r = f.Purchases.CreateReceipt(f.Supplier.Id, "A", new[] { new ReceiptLineInput(f.Oreo.Id, 10, 10, null) }, f.CashierId, false);

        f.Purchases.ConfirmReceipt(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 20000m), f.AdminId);

        Assert.Equal(20000m, f.Db.Products.GetById(f.Oreo.Id)!.AverageCost);
    }

    [Fact]
    public void Surplus_PartlySoldBeforeResolve_AverageStaysBetweenCosts()
    {
        using var f = new PurchaseFixture();
        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId);       // tồn 5, giá 12.000
        var d = Counts.Count(f.Oreo.Id, 15, f.AdminId)!;                 // thừa 10
        f.Sell(f.Oreo, 13);                                              // còn 2

        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 20000, f.AdminId);

        // (5×12.000 + 10×20.000) / 15 = 17.333,33
        Assert.Equal(17333.33m, f.Db.Products.GetById(f.Oreo.Id)!.AverageCost);
    }

    [Fact]
    public void Surplus_PartlySold_LowCost_AverageDoesNotCollapseToZero()
    {
        using var f = new PurchaseFixture();
        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId);
        var d = Counts.Count(f.Oreo.Id, 15, f.AdminId)!;
        f.Sell(f.Oreo, 13);

        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 1000, f.AdminId);

        // (5×12.000 + 10×1.000) / 15 = 4.666,67
        Assert.Equal(4666.67m, f.Db.Products.GetById(f.Oreo.Id)!.AverageCost);
    }

    [Theory]
    [InlineData("HD 001", "HD001")]
    [InlineData("HD001", "H D 0 0 1")]
    [InlineData("hd  001", "HD 001")]
    public void DuplicateInvoice_InternalSpaces_Blocked(string first, string second)
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt(first);

        Assert.Throws<BusinessException>(() => f.AdminReceipt(second));

        Assert.Equal(10, f.Stock(f.Coca));
    }

    [Fact]
    public void CancelReceipt_AfterSales_RestoresAverageAndLastCost()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 20, cost: 10000);
        var typo = f.AdminReceipt("B", qty: 10, cost: 100000); // bình quân 40.000
        f.Sell(f.Coca, 5);

        f.Purchases.CancelReceipt(typo.Id, "Nhập nhầm giá", f.AdminId);

        var p = f.Db.Products.GetById(f.Coca.Id)!;
        Assert.Equal(10000m, p.AverageCost);
        Assert.Equal(10000m, p.LastCost);
    }

    [Fact]
    public void CorrectCosts_NoSales_AverageAndDebtBecomeRight()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var b = f.AdminReceipt("B", qty: 10, cost: 1000);

        f.Purchases.CorrectReceiptCosts(b.Id, b.Lines.ToDictionary(l => l.Id, _ => 120m), "Gõ nhầm", f.AdminId);

        var p = f.Db.Products.GetById(f.Coca.Id)!;
        Assert.Equal(110m, p.AverageCost);
        Assert.Equal(120m, p.LastCost);
        Assert.Equal(20, p.Stock);
        Assert.Equal(1000m + 1200m, f.Purchases.GetSupplierBalances().Single(x => x.Supplier.Id == f.Supplier.Id).Balance);
    }

    [Fact]
    public void CorrectCosts_AfterSales_NoPhantomStock_AverageRight()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var b = f.AdminReceipt("B", qty: 10, cost: 1000); // bình quân 550
        f.Sell(f.Coca, 15);                               // còn 5

        f.Purchases.CorrectReceiptCosts(b.Id, b.Lines.ToDictionary(l => l.Id, _ => 120m), "Gõ nhầm", f.AdminId);

        var p = f.Db.Products.GetById(f.Coca.Id)!;
        Assert.Equal(5, p.Stock);
        Assert.Equal(110m, p.AverageCost);
    }

    [Fact]
    public void CorrectCosts_OlderLot_AfterSalesAndNewerReceipt_AverageRight()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var b = f.AdminReceipt("B", qty: 10, cost: 1000); // bình quân 550
        f.Sell(f.Coca, 15);                               // còn 5
        f.AdminReceipt("C", qty: 10, cost: 550);          // bình quân vẫn 550, tồn 15

        f.Purchases.CorrectReceiptCosts(b.Id, b.Lines.ToDictionary(l => l.Id, _ => 120m), "Gõ nhầm", f.AdminId);

        // Đúng ra: sau B là 110, 5 cái còn lại cộng lô C: (5×110 + 10×550) / 15 = 403,33
        var p = f.Db.Products.GetById(f.Coca.Id)!;
        Assert.Equal(403.33m, p.AverageCost);
        Assert.Equal(550m, p.LastCost);
    }

    [Fact]
    public void CorrectCosts_IsLogged()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("A", qty: 10, cost: 100);

        f.Purchases.CorrectReceiptCosts(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 120m), "Gõ nhầm", f.AdminId);

        var m = f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements[0];
        Assert.Equal(StockMovementType.CostCorrected, m.Type);
        Assert.Equal(0, m.Delta);
        Assert.Equal(f.AdminId, m.UserId);
        Assert.Contains("Gõ nhầm", m.Detail);
    }

    [Fact]
    public void CorrectCosts_ReceiptFromCount_FixesDebtWithoutStockChange()
    {
        using var f = new PurchaseFixture();
        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId);
        var d = Counts.Count(f.Oreo.Id, 8, f.AdminId)!;
        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 160000, f.AdminId);
        var r = f.Purchases.GetReceipts(DateTime.Today, DateTime.Today, null).Single(x => x.InvoiceNo == "CU");

        f.Purchases.CorrectReceiptCosts(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 16000m), "Thừa một số 0", f.AdminId);

        var p = f.Db.Products.GetById(f.Oreo.Id)!;
        Assert.Equal(8, p.Stock);
        Assert.Equal(13500m, p.AverageCost);
        Assert.Equal(48000m, f.Purchases.GetSupplierBalances().Single(x => x.Supplier.Id == f.Supplier.Id).Balance);
    }

    [Fact]
    public void CorrectCosts_BelowWhatWasPaid_Blocked()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("A", qty: 10, cost: 100);
        f.Purchases.PaySupplier(f.Supplier.Id, 1000, null, f.AdminId);

        Assert.Throws<BusinessException>(() => f.Purchases.CorrectReceiptCosts(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 50m), "Giảm", f.AdminId));

        Assert.Equal(0m, f.Purchases.GetSupplierBalances().Single(x => x.Supplier.Id == f.Supplier.Id).Balance);
    }

    [Fact]
    public void CorrectCosts_Invalid_Blocked()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("A", qty: 10, cost: 100);
        var pending = f.CashierReceipt("B");
        var cancelled = f.AdminReceipt("C");
        f.Purchases.CancelReceipt(cancelled.Id, "x", f.AdminId);
        var costs = r.Lines.ToDictionary(l => l.Id, _ => 120m);

        Assert.Throws<BusinessException>(() => f.Purchases.CorrectReceiptCosts(r.Id, costs, " ", f.AdminId));
        Assert.Throws<BusinessException>(() => f.Purchases.CorrectReceiptCosts(r.Id, r.Lines.ToDictionary(l => l.Id, _ => -1m), "x", f.AdminId));
        Assert.Throws<BusinessException>(() => f.Purchases.CorrectReceiptCosts(pending.Id, pending.Lines.ToDictionary(l => l.Id, _ => 1m), "x", f.AdminId));
        Assert.Throws<BusinessException>(() => f.Purchases.CorrectReceiptCosts(cancelled.Id, cancelled.Lines.ToDictionary(l => l.Id, _ => 1m), "x", f.AdminId));
    }

    [Fact]
    public void Upgrade_KeepsEveryRow_ThroughTableRebuilds()
    {
        using var db = new TestDb();
        db.CreateLegacyFile();
        db.ExecuteSql("""
            INSERT INTO Products (Id, Barcode, Name, Category, Price, Stock, IsActive) VALUES (1, 'x', 'SP', '', 1000, 10, 1), (2, 'y', 'SP2', '', 2000, 0, 0);
            INSERT INTO OrderDetails (OrderId, ProductId, Quantity, UnitPrice) VALUES (1, 1, 1, 1000), (1, 2, 2, 2000);
            INSERT INTO StockMovements (ProductId, UserId, OrderId, CreatedAt, Type, BeforeStock, AfterStock, Delta, Reason, UnitPrice) VALUES
              (1, 1, 1, '2026-01-01 08:00:00', 'OutOfBookSale', 1, -1, -2, 'None', 1000);
            """);

        DatabaseUpgrader.Upgrade();

        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM Users"));
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM Orders WHERE Code = 'HD20260101-0001' AND IsCancelled = 0"));
        Assert.Equal(2, db.Scalar("SELECT COUNT(*) FROM Products"));
        Assert.Equal(2, db.Scalar("SELECT COUNT(*) FROM OrderDetails WHERE OrderId = 1 AND CostIsEstimated = 1"));
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM StockMovements WHERE OrderId = 1"));
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }
}
