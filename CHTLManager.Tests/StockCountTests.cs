using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public class StockCountTests
{
    private static StockCountService Counts => new();

    /// <summary>Oreo tồn 5, giá vốn 12.000.</summary>
    private static PurchaseFixture Fixture()
    {
        var f = new PurchaseFixture();
        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId);
        return f;
    }

    private static StockDiscrepancy Reload(int id)
        => new StockCountService().GetDiscrepancies(DateTime.Today, DateTime.Today, null).Single(d => d.Id == id);

    [Fact]
    public void Count_ChangesStockNow_CreatesPendingDiscrepancy_ValuedAtAverageCost()
    {
        using var f = Fixture();

        var d = Counts.Count(f.Oreo.Id, 3, f.CashierId)!;

        Assert.Equal(3, f.Stock(f.Oreo));
        Assert.Equal(DiscrepancyStatus.Pending, d.Status);
        Assert.Equal(5, d.BookStock);
        Assert.Equal(3, d.CountedStock);
        Assert.Equal(-2, d.Delta);
        Assert.Equal(12000m, d.UnitCost);
        Assert.Equal(24000m, d.Value);
        Assert.Equal(f.CashierId, d.CountedByUserId);
        Assert.Equal(1, Counts.CountPending());
    }

    [Fact]
    public void Count_NegativeNumber_Blocked()
    {
        using var f = Fixture();
        Assert.Throws<BusinessException>(() => Counts.Count(f.Oreo.Id, -1, f.AdminId));
        Assert.Equal(5, f.Stock(f.Oreo));
    }

    [Fact]
    public void Count_Match_NoDiscrepancy_StillLogged()
    {
        using var f = Fixture();

        Assert.Null(Counts.Count(f.Oreo.Id, 5, f.AdminId));

        Assert.Equal(0, Counts.CountPending());
        var m = f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements[0];
        Assert.Equal(StockMovementType.StockCount, m.Type);
        Assert.Equal(0, m.Delta);
    }

    [Fact]
    public void Pending_NotInLossExpense()
    {
        using var f = Fixture();
        Counts.Count(f.Oreo.Id, 3, f.AdminId);

        Assert.Equal(0m, f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).LossExpense);
    }

    [Fact]
    public void Shortage_NormalLoss_AddsExpense()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.CashierId)!;

        Counts.ResolveShortage(d.Id, DiscrepancyResolution.NormalLoss, "Hỏng", null, null, null, f.AdminId);

        var r = f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today);
        Assert.Equal(24000m, r.LossExpense);
        Assert.Equal(-24000m, r.NetAfterStockDifferences);
        var saved = Reload(d.Id);
        Assert.Equal(DiscrepancyStatus.Resolved, saved.Status);
        Assert.Equal(f.AdminId, saved.ResolvedByUserId);
        Assert.False(saved.IsSelfResolved);
        Assert.Equal(0, Counts.CountPending());
    }

    [Fact]
    public void Shortage_Compensation_RequiresResponsibleAndAmount()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;

        Assert.Throws<BusinessException>(() => Counts.ResolveShortage(d.Id, DiscrepancyResolution.Compensation, null, null, null, 1000, f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.ResolveShortage(d.Id, DiscrepancyResolution.Compensation, null, f.CashierId, null, null, f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.ResolveShortage(d.Id, DiscrepancyResolution.Compensation, null, f.CashierId, null, 0, f.AdminId));
        Assert.Equal(DiscrepancyStatus.Pending, Reload(d.Id).Status);

        Counts.ResolveShortage(d.Id, DiscrepancyResolution.Compensation, null, f.CashierId, null, 20000, f.AdminId);

        var saved = Reload(d.Id);
        Assert.Equal(20000m, saved.CompensationAmount);
        Assert.Equal(20000m, saved.CompensationRemaining);
        Assert.Equal("Thu ngân", saved.ResponsibleDisplay);
        Assert.Equal(24000m, f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).LossExpense);
    }

    [Fact]
    public void Shortage_Compensation_ResponsibleByNameOutsideSystem()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;

        Counts.ResolveShortage(d.Id, DiscrepancyResolution.Compensation, null, null, "Nhân viên giao hàng", 1000, f.AdminId);

        Assert.Equal("Nhân viên giao hàng", Reload(d.Id).ResponsibleDisplay);
    }

    [Fact]
    public void Shortage_Miscount_RequiresNote_NotExpense_ShownInMiscountValue()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;

        Assert.Throws<BusinessException>(() => Counts.ResolveShortage(d.Id, DiscrepancyResolution.Miscount, " ", null, null, null, f.AdminId));

        Counts.ResolveShortage(d.Id, DiscrepancyResolution.Miscount, "Lần trước đếm nhầm", null, null, null, f.AdminId);

        var r = f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today);
        Assert.Equal(0m, r.LossExpense);
        Assert.Equal(24000m, r.MiscountShortValue);
    }

    [Fact]
    public void Surplus_UnrecordedPurchase_CreatesConfirmedReceipt_DebtUp_StockNotAddedAgain()
    {
        using var f = Fixture();
        f.AdminReceipt("A", qty: 1, cost: 7000);        // Coca tồn 1
        f.Sell(f.Coca, 2, outOfBook: true);             // sổ -1
        var d = Counts.Count(f.Coca.Id, 2, f.AdminId)!; // đếm được 2: lô chưa ghi là 3

        Assert.Equal(3, d.Delta);
        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU-1", 8000, f.AdminId);

        Assert.Equal(2, f.Stock(f.Coca));
        var balance = f.Purchases.GetSupplierBalances().Single(b => b.Supplier.Id == f.Supplier.Id);
        Assert.Equal(7000m + 3 * 8000m, balance.TotalPurchased);
        var receipt = f.Purchases.GetReceipts(DateTime.Today, DateTime.Today, ReceiptStatus.Confirmed).First(r => r.InvoiceNo == "CU-1");
        Assert.Equal(d.Id, receipt.StockDiscrepancyId);
        Assert.Equal(3, receipt.Lines[0].ReceivedQuantity);
    }

    [Fact]
    public void Surplus_UnrecordedPurchase_UpdatesAverageFromOnHandIncrease()
    {
        using var f = Fixture();                            // Oreo tồn 5, giá 12.000
        var d = Counts.Count(f.Oreo.Id, 8, f.AdminId)!;     // thừa 3

        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU-1", 16000, f.AdminId);

        var p = f.Db.Products.GetById(f.Oreo.Id)!;
        Assert.Equal(13500m, p.AverageCost);                // (5×12.000 + 3×16.000) / 8
        Assert.Equal(16000m, p.LastCost);
    }

    [Fact]
    public void Surplus_ResolvedAfterNewLot_AverageMatchesTrueCost()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 1, cost: 100);
        f.Sell(f.Coca, 2, outOfBook: true);               // sổ -1, giá vốn 100
        var d = Counts.Count(f.Coca.Id, 2, f.AdminId)!;   // đếm còn 2
        f.AdminReceipt("MOI", qty: 10, cost: 120);        // lô mới về trước khi Admin xử lý lô cũ

        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 110, f.AdminId);

        // Đúng thực tế: (2×110 + 10×120) / 12 = 118,33
        Assert.InRange(f.Db.Products.GetById(f.Coca.Id)!.AverageCost, 118.30m, 118.36m);
    }

    [Fact]
    public void Surplus_ResolvedBeforeNewLot_AverageMatchesTrueCost()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 1, cost: 100);
        f.Sell(f.Coca, 2, outOfBook: true);
        var d = Counts.Count(f.Coca.Id, 2, f.AdminId)!;

        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 110, f.AdminId);
        Assert.Equal(110m, f.Db.Products.GetById(f.Coca.Id)!.AverageCost);

        f.AdminReceipt("MOI", qty: 10, cost: 120);
        Assert.Equal(118.33m, f.Db.Products.GetById(f.Coca.Id)!.AverageCost);
    }

    [Fact]
    public void Surplus_AllSoldBeforeResolve_KeepsAverage_SetsLastCost()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("A", qty: 1, cost: 100);
        f.Sell(f.Coca, 2, outOfBook: true);
        var d = Counts.Count(f.Coca.Id, 2, f.AdminId)!;
        f.Sell(f.Coca, 2);

        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 110, f.AdminId);

        var p = f.Db.Products.GetById(f.Coca.Id)!;
        Assert.Equal(100m, p.AverageCost);
        Assert.Equal(110m, p.LastCost);
    }

    [Fact]
    public void Surplus_UnrecordedPurchase_RequiresSupplierInvoiceCost_NoDuplicateInvoice()
    {
        using var f = Fixture();
        f.AdminReceipt("TRUNG");
        var d = Counts.Count(f.Oreo.Id, 8, f.AdminId)!;

        Assert.Throws<BusinessException>(() => Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, null, "X", 1000, f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, " ", 1000, f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "X", null, f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "trung", 1000, f.AdminId));

        Assert.Equal(DiscrepancyStatus.Pending, Reload(d.Id).Status);
    }

    [Fact]
    public void ReceiptFromCount_CannotBeCancelled()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 8, f.AdminId)!;
        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU-1", 16000, f.AdminId);
        var receipt = f.Purchases.GetReceipts(DateTime.Today, DateTime.Today, null).Single(r => r.InvoiceNo == "CU-1");

        Assert.Throws<BusinessException>(() => f.Purchases.CancelReceipt(receipt.Id, "Thử", f.AdminId));

        Assert.Equal(8, f.Stock(f.Oreo));
    }

    [Fact]
    public void Surplus_OtherIncome_AddsIncome()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 7, f.AdminId)!;

        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.OtherIncome, null, null, null, null, f.AdminId);

        var r = f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today);
        Assert.Equal(24000m, r.OtherIncome);
        Assert.Equal(24000m, r.NetAfterStockDifferences);
    }

    [Fact]
    public void Surplus_Miscount_RequiresNote_ShownInMiscountValue()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 7, f.AdminId)!;

        Assert.Throws<BusinessException>(() => Counts.ResolveSurplus(d.Id, DiscrepancyResolution.Miscount, null, null, null, null, f.AdminId));
        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.Miscount, "Đếm nhầm", null, null, null, f.AdminId);

        Assert.Equal(24000m, f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).MiscountSurplusValue);
    }

    [Fact]
    public void WrongResolutionForDirection_Blocked()
    {
        using var f = Fixture();
        var shortage = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        var surplus = Counts.Count(f.Oreo.Id, 6, f.AdminId)!;

        Assert.Throws<BusinessException>(() => Counts.ResolveShortage(surplus.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.ResolveSurplus(shortage.Id, DiscrepancyResolution.OtherIncome, null, null, null, null, f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.ResolveShortage(shortage.Id, DiscrepancyResolution.OtherIncome, null, null, null, null, f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.ResolveSurplus(surplus.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId));
    }

    [Fact]
    public void ResolveTwice_Blocked()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        Counts.ResolveShortage(d.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId);

        Assert.Throws<BusinessException>(() => Counts.ResolveShortage(d.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId));
    }

    [Fact]
    public void Collect_ReducesRemaining_AddsToProfit()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        Counts.ResolveShortage(d.Id, DiscrepancyResolution.Compensation, null, f.CashierId, null, 20000, f.AdminId);

        Counts.CollectCompensation(d.Id, 5000, "Lần 1", f.AdminId);

        Assert.Equal(15000m, Reload(d.Id).CompensationRemaining);
        var r = f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today);
        Assert.Equal(5000m, r.CompensationCollected);
        Assert.Equal(-24000m + 5000m, r.NetAfterStockDifferences);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(20001)]
    public void Collect_OverAmount_Zero_Negative_Blocked(int amount)
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        Counts.ResolveShortage(d.Id, DiscrepancyResolution.Compensation, null, f.CashierId, null, 20000, f.AdminId);

        Assert.Throws<BusinessException>(() => Counts.CollectCompensation(d.Id, amount, null, f.AdminId));

        Assert.Equal(20000m, Reload(d.Id).CompensationRemaining);
    }

    [Fact]
    public void Collect_WithoutCompensation_Blocked()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        Assert.Throws<BusinessException>(() => Counts.CollectCompensation(d.Id, 1000, null, f.AdminId));
    }

    [Fact]
    public void SameCounterAndResolver_FlaggedSelfResolved()
    {
        using var f = Fixture();
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;

        Counts.ResolveShortage(d.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId);

        Assert.True(Reload(d.Id).IsSelfResolved);
    }

    [Fact]
    public void GetDiscrepancies_FiltersStatusAndDate()
    {
        using var f = Fixture();
        var a = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        Counts.Count(f.Oreo.Id, 2, f.AdminId);
        Counts.ResolveShortage(a.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId);

        Assert.Single(Counts.GetDiscrepancies(DateTime.Today, DateTime.Today, DiscrepancyStatus.Pending));
        Assert.Equal(2, Counts.GetDiscrepancies(DateTime.Today, DateTime.Today, null).Count);
        Assert.Empty(Counts.GetDiscrepancies(DateTime.Today.AddDays(-5), DateTime.Today.AddDays(-4), null));
    }

    [Fact]
    public void FullScenario_OldLotThenNewLot_Stock12_TwoReceipts_CorrectDebt()
    {
        using var f = Fixture();
        f.Sell(f.Coca, 1, outOfBook: true);             // sổ 0, bán 1 ngoài sổ
        var d = Counts.Count(f.Coca.Id, 2, f.CashierId)!; // đếm còn 2: lô cũ 3 cái
        Counts.ResolveSurplus(d.Id, DiscrepancyResolution.UnrecordedPurchase, null, f.Supplier.Id, "CU", 7000, f.AdminId);

        f.AdminReceipt("MOI", qty: 10, cost: 7000);

        Assert.Equal(12, f.Stock(f.Coca));
        Assert.Equal(2, f.Purchases.GetReceipts(DateTime.Today, DateTime.Today, ReceiptStatus.Confirmed).Count);
        Assert.Equal(13 * 7000m, f.Purchases.GetSupplierBalances().Single(b => b.Supplier.Id == f.Supplier.Id).Balance);
    }

    [Fact]
    public void LegacyStockCounts_BecomeResolvedDiscrepancies()
    {
        using var db = new TestDb();
        db.CreateLegacyFile();
        db.ExecuteSql("""
            INSERT INTO Products (Id, Barcode, Name, Category, Price, Stock, IsActive) VALUES (1, 'x', 'SP', '', 1000, 10, 1);
            INSERT INTO StockMovements (ProductId, UserId, CreatedAt, Type, BeforeStock, AfterStock, Delta, Reason, UnitPrice) VALUES
              (1, 1, '2026-01-01 08:00:00', 'StockCount', 12, 10, -2, 'Lost', 1000),
              (1, 1, '2026-01-02 08:00:00', 'StockCount', 10, 10, 0, 'None', 1000),
              (1, 1, '2026-01-03 08:00:00', 'StockCount', 8, 10, 2, 'UnrecordedImport', 1000);
            """);

        DatabaseUpgrader.Upgrade();

        Assert.Equal(2, db.Scalar("SELECT COUNT(*) FROM StockDiscrepancies WHERE Status = 'Resolved'"));
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM StockDiscrepancies WHERE Resolution = 'NormalLoss' AND Delta = -2"));
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM StockDiscrepancies WHERE Resolution = 'OtherIncome' AND Delta = 2"));
    }
}
