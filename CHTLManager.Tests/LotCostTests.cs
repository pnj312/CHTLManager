using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

/// <summary>Giá vốn khi hủy hoặc sửa một lô giữa các lô khác, và khi có hàng đang chờ giá. Số kỳ vọng tính tay.</summary>
public class LotCostTests
{
    private static StockCountService Counts => new();
    private static decimal Avg(PurchaseFixture f, Product p) => f.Db.Products.GetById(p.Id)!.AverageCost;
    private static Dictionary<int, decimal> Cost(PurchaseReceipt r, decimal c) => r.Lines.ToDictionary(l => l.Id, _ => c);

    // ===== Hủy lô =====

    [Fact]
    public void Cancel_OldestLot_LeavesNewestCost()
    {
        using var f = new PurchaseFixture();
        var a = f.AdminReceipt("A", 10, 100);
        f.AdminReceipt("B", 10, 300);

        f.Purchases.CancelReceipt(a.Id, "x", f.AdminId);

        Assert.Equal(300m, Avg(f, f.Coca));
    }

    [Fact]
    public void Cancel_MiddleLot_AverageOfTheOthers()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 100);
        var b = f.AdminReceipt("B", 10, 300);
        f.AdminReceipt("C", 10, 800);

        f.Purchases.CancelReceipt(b.Id, "x", f.AdminId);

        Assert.Equal(450m, Avg(f, f.Coca)); // (10×100 + 10×800) / 20
    }

    [Fact]
    public void Cancel_OldestLot_AfterSales()
    {
        using var f = new PurchaseFixture();
        var a = f.AdminReceipt("A", 10, 100);
        f.AdminReceipt("B", 10, 300);
        f.Sell(f.Coca, 5);

        f.Purchases.CancelReceipt(a.Id, "x", f.AdminId);

        Assert.Equal(300m, Avg(f, f.Coca));
    }

    [Fact]
    public void Cancel_LotSoldThroughBeforeNewerReceipt_NewerCostWins()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 100);
        var b = f.AdminReceipt("B", 10, 300); // 200
        f.Sell(f.Coca, 15);                   // còn 5
        f.AdminReceipt("C", 10, 800);         // (5×200 + 10×800) / 15 = 600

        f.Purchases.CancelReceipt(b.Id, "x", f.AdminId); // tồn 15 - 10 = 5

        Assert.Equal(800m, Avg(f, f.Coca));
    }

    [Fact]
    public void Cancel_LotBeforeReceiptOnEmptyShelf_AverageUnchanged()
    {
        using var f = new PurchaseFixture();
        var a = f.AdminReceipt("A", 10, 100);
        f.Sell(f.Coca, 10);
        f.AdminReceipt("B", 10, 300); // kệ trống nên lô A không còn trong bình quân

        f.Purchases.CancelReceipt(a.Id, "x", f.AdminId);

        Assert.Equal(300m, Avg(f, f.Coca));
    }

    [Fact]
    public void Cancel_TwoLotsInARow_LeavesTheLastLot()
    {
        using var f = new PurchaseFixture();
        var a = f.AdminReceipt("A", 10, 100);
        var b = f.AdminReceipt("B", 10, 300);
        f.AdminReceipt("C", 10, 800);

        f.Purchases.CancelReceipt(b.Id, "x", f.AdminId);
        f.Purchases.CancelReceipt(a.Id, "y", f.AdminId);

        Assert.Equal(800m, Avg(f, f.Coca));
        Assert.Equal(10, f.Stock(f.Coca));
    }

    [Fact]
    public void Cancel_OldDataWithoutCostBase_UsesStockLog()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 100);
        var b = f.AdminReceipt("B", 10, 300);
        f.Db.ExecuteSql("UPDATE PurchaseReceiptLines SET CostBaseStock = NULL");

        f.Purchases.CancelReceipt(b.Id, "x", f.AdminId);

        Assert.Equal(100m, Avg(f, f.Coca));
    }

    // ===== Hàng chờ giá =====

    [Fact]
    public void TwoPendingReceipts_OldAverageDoesNotLeakIn()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 50);
        f.Sell(f.Coca, 10);
        var b = f.CashierReceipt("B", 10);
        var c = f.CashierReceipt("C", 10);

        f.Purchases.ConfirmReceipt(b.Id, Cost(b, 100), f.AdminId);
        Assert.Equal(100m, Avg(f, f.Coca));

        f.Purchases.ConfirmReceipt(c.Id, Cost(c, 200), f.AdminId);
        Assert.Equal(150m, Avg(f, f.Coca));
    }

    [Fact]
    public void PendingThenAdminReceipt_OldAverageDoesNotLeakIn()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 50);
        f.Sell(f.Coca, 10);
        var b = f.CashierReceipt("B", 10);
        f.AdminReceipt("C", 10, 200);
        Assert.Equal(200m, Avg(f, f.Coca));

        f.Purchases.ConfirmReceipt(b.Id, Cost(b, 100), f.AdminId);

        Assert.Equal(150m, Avg(f, f.Coca));
    }

    [Fact]
    public void Pending_WithOldStock_StillBlendsPricedUnits()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 50);
        var b = f.CashierReceipt("B", 10);
        var c = f.CashierReceipt("C", 10);

        f.Purchases.ConfirmReceipt(b.Id, Cost(b, 100), f.AdminId);

        Assert.Equal(75m, Avg(f, f.Coca)); // chỉ 10 cái giá 50 và 10 cái giá 100, lô C chưa có giá
        f.Purchases.ConfirmReceipt(c.Id, Cost(c, 150), f.AdminId);
        Assert.Equal(100m, Avg(f, f.Coca));
    }

    [Fact]
    public void Cancel_ConfirmedLot_WithPendingAround()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 50);
        var b = f.CashierReceipt("B", 10);
        f.CashierReceipt("C", 10);
        f.Purchases.ConfirmReceipt(b.Id, Cost(b, 100), f.AdminId); // 75

        f.Purchases.CancelReceipt(b.Id, "x", f.AdminId);

        Assert.Equal(50m, Avg(f, f.Coca));
    }

    // ===== Sửa giá =====

    [Fact]
    public void Correct_LotConfirmedAfterSales_UsesStockAtConfirm()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 50);
        var b = f.CashierReceipt("B", 10);
        f.Sell(f.Coca, 5); // còn 15, trong đó 5 cái giá 50
        f.Purchases.ConfirmReceipt(b.Id, Cost(b, 1000), f.AdminId); // (5×50 + 10×1000) / 15 = 683,33

        f.Purchases.CorrectReceiptCosts(b.Id, Cost(b, 100), "Gõ nhầm", f.AdminId);

        Assert.Equal(83.33m, Avg(f, f.Coca)); // (5×50 + 10×100) / 15
    }

    [Fact]
    public void Correct_FirstLotOnStockWithUnknownCost_TakesCorrectedCost()
    {
        using var f = new PurchaseFixture(); // Oreo tồn 5, chưa có giá vốn
        var r = f.Purchases.CreateReceipt(f.Supplier.Id, "A", new[] { new ReceiptLineInput(f.Oreo.Id, 10, 10, 20000) }, f.AdminId, true);

        f.Purchases.CorrectReceiptCosts(r.Id, Cost(r, 18000), "Gõ nhầm", f.AdminId);

        Assert.Equal(18000m, Avg(f, f.Oreo));
    }

    [Fact]
    public void Correct_MiddleLot_ThroughNewerReceiptOnLowStock()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 100);
        var b = f.AdminReceipt("B", 10, 1000);
        f.Sell(f.Coca, 15);
        f.AdminReceipt("C", 10, 2000);

        f.Purchases.CorrectReceiptCosts(b.Id, Cost(b, 120), "Gõ nhầm", f.AdminId);

        Assert.Equal(1370m, Avg(f, f.Coca)); // (5×110 + 10×2000) / 15 = 1370
    }

    [Fact]
    public void CorrectThenCancel_BackToOriginal()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 100);
        var b = f.AdminReceipt("B", 10, 1000);
        f.AdminReceipt("C", 10, 400);

        f.Purchases.CorrectReceiptCosts(b.Id, Cost(b, 200), "Gõ nhầm", f.AdminId);
        Assert.Equal(233.33m, Avg(f, f.Coca)); // (10×100 + 10×200 + 10×400) / 30

        f.Purchases.CancelReceipt(b.Id, "Trả hàng", f.AdminId);
        Assert.Equal(250m, Avg(f, f.Coca));
    }

    [Fact]
    public void Correct_OlderLot_ThroughLotFromStockCount()
    {
        using var f = new PurchaseFixture();
        var a = f.AdminReceipt("A", 10, 1000);
        var d = Counts.Count(f.Coca.Id, 20, f.AdminId)!; // thừa 10, đang tính theo giá 1000
        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 500, f.AdminId); // 750

        f.Purchases.CorrectReceiptCosts(a.Id, Cost(a, 100), "Gõ nhầm", f.AdminId);

        Assert.Equal(300m, Avg(f, f.Coca)); // (10×100 + 10×500) / 20
    }

    // ===== Bán hàng chờ giá =====

    [Fact]
    public void Sale_DippingIntoPendingGoods_FlaggedEstimated()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 50);
        f.Sell(f.Coca, 10);
        f.CashierReceipt("B", 10);

        var o = f.Sell(f.Coca, 5);

        Assert.True(o.Details[0].CostIsEstimated);
        Assert.Equal(1, f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).EstimatedLineCount);
    }

    [Fact]
    public void Sale_PartlyFromPendingGoods_FlaggedEstimated()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 3, 50);
        f.CashierReceipt("B", 10);

        var o = f.Sell(f.Coca, 5); // 3 cái có giá, 2 cái chờ giá

        Assert.True(o.Details[0].CostIsEstimated);
    }

    [Fact]
    public void Sale_OnlyFromPricedGoods_NotFlagged_EvenWithPendingAround()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", 10, 50);
        f.CashierReceipt("B", 10);

        var o = f.Sell(f.Coca, 10);

        Assert.False(o.Details[0].CostIsEstimated);
    }

    [Fact]
    public void Sale_AfterConfirm_NotFlagged()
    {
        using var f = new PurchaseFixture();
        var b = f.CashierReceipt("B", 10);
        f.Purchases.ConfirmReceipt(b.Id, Cost(b, 100), f.AdminId);

        Assert.False(f.Sell(f.Coca, 5).Details[0].CostIsEstimated);
    }

    // ===== Thất thoát khi chưa có giá vốn =====

    [Theory]
    [InlineData(DiscrepancyResolution.NormalLoss)]
    [InlineData(DiscrepancyResolution.Compensation)]
    public void Shortage_NoKnownCost_Blocked_StaysPending(DiscrepancyResolution resolution)
    {
        using var f = new PurchaseFixture(); // Oreo chưa có giá vốn
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;

        var ex = Assert.Throws<BusinessException>(() =>
            Counts.ResolveShortage(d.Id, resolution, null, f.CashierId, null, 5000, f.AdminId));

        Assert.Contains("giá vốn", ex.Message);
        Assert.Equal(1, Counts.CountPending());
    }

    [Fact]
    public void Shortage_CostDeclaredAfterCount_ValuedAtDeclaredCost()
    {
        using var f = new PurchaseFixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId);

        Counts.ResolveShortage(d.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId);

        Assert.Equal(24000m, f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).LossExpense);
    }

    [Fact]
    public void Shortage_ReceiptAfterCount_ValuedAtReceiptCost()
    {
        using var f = new PurchaseFixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        f.Purchases.CreateReceipt(f.Supplier.Id, "A", new[] { new ReceiptLineInput(f.Oreo.Id, 10, 10, 20000) }, f.AdminId, true);

        Counts.ResolveShortage(d.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId);

        Assert.Equal(40000m, f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).LossExpense);
    }

    [Fact]
    public void Shortage_NoKnownCost_MiscountStillAllowed()
    {
        using var f = new PurchaseFixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;

        Counts.ResolveShortage(d.Id, DiscrepancyResolution.Miscount, "Đếm sai", null, null, null, f.AdminId);

        Assert.Equal(0, Counts.CountPending());
    }

    [Fact]
    public void Surplus_NoKnownCost_OtherIncomeBlocked_UnrecordedLotAllowed()
    {
        using var f = new PurchaseFixture();
        var d = Counts.Count(f.Oreo.Id, 8, f.AdminId)!;

        Assert.Throws<BusinessException>(() =>
            Counts.ResolveSurplus(d.Id, DiscrepancyResolution.OtherIncome, null, null, null, null, f.AdminId));

        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 16000, f.AdminId);
        Assert.Equal(16000m, Avg(f, f.Oreo));
    }
}
