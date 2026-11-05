using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public class ProfitTests
{
    private static Product Reload(PurchaseFixture f, Product p) => f.Db.Products.GetById(p.Id)!;

    [Fact]
    public void AdminReceipt_UpdatesAverageAndLastCost()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        f.AdminReceipt("B", qty: 10, cost: 120);

        var coca = Reload(f, f.Coca);
        Assert.Equal(110m, coca.AverageCost);
        Assert.Equal(120m, coca.LastCost);
    }

    [Fact]
    public void FreeGoods_LowerAverage_KeepLastCost()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        f.AdminReceipt("B", qty: 10, cost: 0);

        var coca = Reload(f, f.Coca);
        Assert.Equal(50m, coca.AverageCost);
        Assert.Equal(100m, coca.LastCost);
    }

    [Fact]
    public void PendingReceipt_DoesNotChangeCost()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        f.CashierReceipt("B", qty: 10);

        Assert.Equal(100m, Reload(f, f.Coca).AverageCost);
    }

    [Fact]
    public void ConfirmReceipt_UpdatesAverage()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var r = f.CashierReceipt("B", qty: 10);

        f.Purchases.ConfirmReceipt(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 120m), f.AdminId);

        var coca = Reload(f, f.Coca);
        Assert.Equal(110m, coca.AverageCost);
        Assert.Equal(120m, coca.LastCost);
    }

    [Fact]
    public void ConfirmAfterSales_UsesStockExcludingThisReceipt()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var r = f.CashierReceipt("B", qty: 10);
        f.Sell(f.Coca, 15); // còn 5: phần ngoài phiếu này coi như 0 (đã bán hết hàng cũ)

        f.Purchases.ConfirmReceipt(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 120m), f.AdminId);

        Assert.Equal(120m, Reload(f, f.Coca).AverageCost);
    }

    [Fact]
    public void CancelConfirmedReceipt_RemovesFromAverage()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var b = f.AdminReceipt("B", qty: 10, cost: 120);

        f.Purchases.CancelReceipt(b.Id, "Nhầm", f.AdminId);

        Assert.Equal(100m, Reload(f, f.Coca).AverageCost);
    }

    [Fact]
    public void CancelPendingReceipt_KeepsAverage()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var b = f.CashierReceipt("B", qty: 10);

        f.Purchases.CancelReceipt(b.Id, "Nhầm", f.AdminId);

        Assert.Equal(100m, Reload(f, f.Coca).AverageCost);
    }

    [Fact]
    public void CreateOrder_StoresUnitCost_NotAffectedByLaterImport()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var order = f.Sell(f.Coca, 2);

        f.AdminReceipt("B", qty: 10, cost: 500);

        var line = f.Db.Orders.GetById(order.Id)!.Details[0];
        Assert.Equal(100m, line.UnitCost);
        Assert.False(line.CostIsEstimated);
        Assert.Equal(200m, line.LineCost);
    }

    [Fact]
    public void OutOfBookSale_CostFlaggedEstimated_UsesAverage()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 1, cost: 100);

        var line = f.Sell(f.Coca, 3, outOfBook: true).Details[0];

        Assert.Equal(100m, line.UnitCost);
        Assert.True(line.CostIsEstimated);
    }

    [Fact]
    public void NoAverage_FallsBackToLastCost_Estimated()
    {
        using var f = new PurchaseFixture();
        using (var db = new CHTLManager.DAL.AppDbContext())
        {
            var p = db.Products.First(x => x.Id == f.Coca.Id);
            p.Stock = 5; p.AverageCost = 0; p.LastCost = 90;
            db.SaveChanges();
        }

        var line = f.Sell(f.Coca, 1).Details[0];

        Assert.Equal(90m, line.UnitCost);
        Assert.True(line.CostIsEstimated);
    }

    [Fact]
    public void NoCostAtAll_ZeroEstimated()
    {
        using var f = new PurchaseFixture();
        var line = f.Sell(f.Oreo, 1).Details[0];

        Assert.Equal(0m, line.UnitCost);
        Assert.True(line.CostIsEstimated);
    }

    [Fact]
    public void Add_WithOpeningCost_SetsAverage()
    {
        using var f = new PurchaseFixture();
        var p = f.Db.Products.Add(new Product { Barcode = "9", Name = "Mới", Price = 100, Stock = 5, AverageCost = 70 }, f.AdminId);

        Assert.Equal(70m, Reload(f, p).AverageCost);
        Assert.Equal(70m, Reload(f, p).LastCost);
    }

    [Fact]
    public void Add_NegativeOpeningCost_Blocked()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Db.Products.Add(new Product { Barcode = "9", Name = "Mới", Price = 100, Stock = 5, AverageCost = -1 }, f.AdminId));
    }

    [Fact]
    public void SetOpeningCost_Works_LoggedOnce_ThenBlocked()
    {
        using var f = new PurchaseFixture();

        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId);

        Assert.Equal(12000m, Reload(f, f.Oreo).AverageCost);
        var m = f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements[0];
        Assert.Equal(StockMovementType.CostDeclared, m.Type);
        Assert.Equal(0, m.Delta);
        Assert.Contains("12.000", m.Detail.Replace(",", "."));

        Assert.Throws<BusinessException>(() => f.Db.Products.SetOpeningCost(f.Oreo.Id, 1, f.AdminId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SetOpeningCost_NonPositive_Blocked(int cost)
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Db.Products.SetOpeningCost(f.Oreo.Id, cost, f.AdminId));
    }

    [Fact]
    public void ProfitReport_MatchesHandCalculation()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 7000);         // Coca giá vốn 7.000, bán 10.000
        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId); // Oreo giá vốn 12.000, bán 16.000
        f.Sell(f.Coca, 3);
        f.Sell(f.Oreo, 2);

        var r = f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today);

        Assert.Equal(62000m, r.Revenue);       // 3×10.000 + 2×16.000
        Assert.Equal(45000m, r.CostOfGoods);   // 3×7.000 + 2×12.000
        Assert.Equal(17000m, r.GrossProfit);
        Assert.Equal(0, r.EstimatedLineCount);
        var coca = Assert.Single(r.Products, p => p.Name == "Coca");
        Assert.Equal(3, coca.Quantity);
        Assert.Equal(30000m, coca.Revenue);
        Assert.Equal(21000m, coca.Cost);
        Assert.Equal(9000m, coca.Profit);
    }

    [Fact]
    public void ProfitReport_ExcludesOtherDates()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 7000);
        f.Sell(f.Coca, 3);

        var r = f.Db.Orders.GetProfitReport(DateTime.Today.AddDays(-3), DateTime.Today.AddDays(-1));

        Assert.Equal(0m, r.Revenue);
        Assert.Empty(r.Products);
    }

    [Fact]
    public void ProfitReport_NoCost_CountsEstimatedLines()
    {
        using var f = new PurchaseFixture();
        f.Sell(f.Oreo, 1);

        var r = f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today);

        Assert.Equal(1, r.EstimatedLineCount);
        Assert.Equal(16000m, r.GrossProfit);
    }

    [Fact]
    public void ProfitReport_FromAfterTo_Blocked()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today.AddDays(-1)));
    }

    [Fact]
    public void LegacyOrders_AfterUpgrade_AreEstimated()
    {
        using var db = new TestDb();
        db.CreateLegacyFile();
        db.ExecuteSql("""
            INSERT INTO Products (Id, Barcode, Name, Category, Price, Stock, IsActive) VALUES (1, 'x', 'SP', '', 1000, 10, 1);
            INSERT INTO OrderDetails (OrderId, ProductId, Quantity, UnitPrice) VALUES (1, 1, 1, 1000);
            """);

        DatabaseUpgrader.Upgrade();

        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM OrderDetails WHERE CostIsEstimated = 1"));
    }
}
