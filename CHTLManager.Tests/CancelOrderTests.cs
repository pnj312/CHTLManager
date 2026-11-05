using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public class CancelOrderTests
{
    [Fact]
    public void Cancel_RestoresStock_LogsMovement_KeepsOrderInHistory()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 7000);
        var order = f.Sell(f.Coca, 3);

        f.Db.Orders.CancelOrder(order.Id, "Quét nhầm", f.AdminId);

        Assert.Equal(10, f.Stock(f.Coca));
        var saved = f.Db.Orders.GetById(order.Id)!;
        Assert.True(saved.IsCancelled);
        Assert.Equal("Quét nhầm", saved.CancelReason);
        Assert.Equal(f.AdminId, saved.CancelledByUserId);
        Assert.NotNull(saved.CancelledAt);
        Assert.Contains(f.Db.Orders.GetHistory(), o => o.Id == order.Id);

        var m = f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements[0];
        Assert.Equal(StockMovementType.SaleCancelled, m.Type);
        Assert.Equal(3, m.Delta);
        Assert.Equal(order.Id, m.OrderId);
        Assert.Equal(f.AdminId, m.UserId);
    }

    [Fact]
    public void Cancel_ExcludedFromRevenueAndProfit()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 7000);
        var cancelled = f.Sell(f.Coca, 3);
        f.Sell(f.Coca, 2);

        f.Db.Orders.CancelOrder(cancelled.Id, "Quét nhầm", f.AdminId);

        var revenue = f.Db.Orders.GetRevenueReport(DateTime.Today, DateTime.Today);
        Assert.Equal(20000m, revenue.TotalRevenue);
        Assert.Equal(1, revenue.OrderCount);
        Assert.Equal(2, revenue.ItemsSold);
        Assert.Equal(20000m, revenue.Daily[0].Revenue);
        Assert.Equal(2, revenue.Orders.Count); // vẫn liệt kê để đối chiếu

        var profit = f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today);
        Assert.Equal(20000m, profit.Revenue);
        Assert.Equal(14000m, profit.CostOfGoods);
        Assert.Equal(2, Assert.Single(profit.Products).Quantity);
    }

    [Fact]
    public void Cancel_RequiresReason()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10);
        var order = f.Sell(f.Coca, 3);

        Assert.Throws<BusinessException>(() => f.Db.Orders.CancelOrder(order.Id, "  ", f.AdminId));

        Assert.Equal(7, f.Stock(f.Coca));
        Assert.False(f.Db.Orders.GetById(order.Id)!.IsCancelled);
    }

    [Fact]
    public void CancelTwice_Blocked_StockRestoredOnce()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10);
        var order = f.Sell(f.Coca, 3);
        f.Db.Orders.CancelOrder(order.Id, "Quét nhầm", f.AdminId);

        Assert.Throws<BusinessException>(() => f.Db.Orders.CancelOrder(order.Id, "Lại", f.AdminId));

        Assert.Equal(10, f.Stock(f.Coca));
    }

    [Fact]
    public void Cancel_OrderOfDiscontinuedProduct_Works()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 3);
        var order = f.Sell(f.Coca, 3);
        f.Db.Products.Delete(f.Coca.Id);

        f.Db.Orders.CancelOrder(order.Id, "Trả lại", f.AdminId);

        Assert.True(f.Db.Orders.GetById(order.Id)!.IsCancelled);
        using var db = new CHTLManager.DAL.AppDbContext();
        Assert.Equal(3, db.Products.First(p => p.Id == f.Coca.Id).Stock);
    }

    [Fact]
    public void Cancel_OutOfBookOrder_ReturnsLedgerToBefore()
    {
        using var f = new PurchaseFixture();
        var order = f.Sell(f.Coca, 2, outOfBook: true);
        Assert.Equal(-2, f.Stock(f.Coca));

        f.Db.Orders.CancelOrder(order.Id, "Khách đổi ý", f.AdminId);

        Assert.Equal(0, f.Stock(f.Coca));
        Assert.Equal(0, f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).OutOfBookQuantity);
    }

    [Fact]
    public void Cancel_AddsBackAtStoredCost()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10, cost: 100);
        var order = f.Sell(f.Coca, 5);                // bán 5 giá vốn 100, còn 5
        f.AdminReceipt("B", qty: 5, cost: 200);       // 10 cái, bình quân 150

        f.Db.Orders.CancelOrder(order.Id, "Trả lại", f.AdminId);

        var p = f.Db.Products.GetById(f.Coca.Id)!;
        Assert.Equal(15, p.Stock);
        Assert.Equal(133.33m, p.AverageCost);         // (10×150 + 5×100) / 15
    }

    [Fact]
    public void NewOrderCode_AfterCancel_DoesNotCollide()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 10);
        var first = f.Sell(f.Coca, 1);
        f.Db.Orders.CancelOrder(first.Id, "Nhầm", f.AdminId);

        var second = f.Sell(f.Coca, 1);

        Assert.NotEqual(first.Code, second.Code);
    }

    [Fact]
    public void Cancel_UnknownOrder_Blocked()
    {
        using var f = new PurchaseFixture();
        Assert.Throws<BusinessException>(() => f.Db.Orders.CancelOrder(999, "x", f.AdminId));
    }
}
