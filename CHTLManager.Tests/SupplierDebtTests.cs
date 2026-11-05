using CHTLManager.BLL;
using CHTLManager.BLL.Interfaces;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public class SupplierDebtTests
{
    private static SupplierBalance Balance(PurchaseFixture f)
        => f.Purchases.GetSupplierBalances().Single(b => b.Supplier.Id == f.Supplier.Id);

    [Fact]
    public void ConfirmedReceipt_IncreasesDebt_ByReceivedTimesCost()
    {
        using var f = new PurchaseFixture();

        f.Purchases.CreateReceipt(f.Supplier.Id, "HD1", new[] { new ReceiptLineInput(f.Coca.Id, 10, 9, 100) }, f.AdminId, true);

        var b = Balance(f);
        Assert.Equal(900m, b.TotalPurchased);
        Assert.Equal(0m, b.TotalPaid);
        Assert.Equal(900m, b.Balance);
    }

    [Fact]
    public void PendingReceipt_NotInDebt_UntilConfirmed()
    {
        using var f = new PurchaseFixture();
        var r = f.CashierReceipt("HD1", qty: 10);
        Assert.Equal(0m, Balance(f).Balance);

        f.Purchases.ConfirmReceipt(r.Id, r.Lines.ToDictionary(l => l.Id, _ => 100m), f.AdminId);

        Assert.Equal(1000m, Balance(f).Balance);
    }

    [Fact]
    public void CancelledReceipt_NotInDebt()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("HD1", qty: 10, cost: 100);

        f.Purchases.CancelReceipt(r.Id, "Nhầm", f.AdminId);

        Assert.Equal(0m, Balance(f).Balance);
    }

    [Fact]
    public void Payment_ReducesBalance()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1", qty: 10, cost: 100);

        var p = f.Purchases.PaySupplier(f.Supplier.Id, 400, "Tiền mặt", f.AdminId);

        var b = Balance(f);
        Assert.Equal(400m, b.TotalPaid);
        Assert.Equal(600m, b.Balance);
        Assert.Equal(400m, p.Amount);
        Assert.Equal(f.AdminId, p.UserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Payment_NonPositive_Blocked(int amount)
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1", qty: 10, cost: 100);

        Assert.Throws<BusinessException>(() => f.Purchases.PaySupplier(f.Supplier.Id, amount, null, f.AdminId));

        Assert.Equal(1000m, Balance(f).Balance);
    }

    [Fact]
    public void Payment_OverBalance_Blocked_BalanceUnchanged()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1", qty: 10, cost: 100);

        var ex = Assert.Throws<BusinessException>(() => f.Purchases.PaySupplier(f.Supplier.Id, 1001, null, f.AdminId));

        Assert.Contains("1", ex.Message);
        Assert.Equal(1000m, Balance(f).Balance);
    }

    [Fact]
    public void Payment_ExactBalance_Allowed()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1", qty: 10, cost: 100);

        f.Purchases.PaySupplier(f.Supplier.Id, 1000, null, f.AdminId);

        Assert.Equal(0m, Balance(f).Balance);
    }

    [Fact]
    public void CancelConfirmedReceipt_AfterFullPayment_BlockedUntilResolved()
    {
        using var f = new PurchaseFixture();
        var r = f.AdminReceipt("HD1", qty: 10, cost: 100);
        var p = f.Purchases.PaySupplier(f.Supplier.Id, 1000, null, f.AdminId);

        var ex = Assert.Throws<BusinessException>(() => f.Purchases.CancelReceipt(r.Id, "Nhầm", f.AdminId));
        Assert.Contains("đã trả", ex.Message);
        Assert.Equal(10, f.Stock(f.Coca));

        f.Purchases.CancelPayment(p.Id, "Nhà cung cấp hoàn tiền", f.AdminId);
        f.Purchases.CancelReceipt(r.Id, "Nhầm", f.AdminId);

        Assert.Equal(0, f.Stock(f.Coca));
        Assert.Equal(0m, Balance(f).Balance);
    }

    [Fact]
    public void CancelPendingReceipt_NotAffectedByPayments()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1", qty: 10, cost: 100);
        f.Purchases.PaySupplier(f.Supplier.Id, 1000, null, f.AdminId);
        var pending = f.CashierReceipt("HD2");

        f.Purchases.CancelReceipt(pending.Id, "Sai hàng", f.AdminId);

        Assert.Equal(0m, Balance(f).Balance);
    }

    [Fact]
    public void CancelPayment_RestoresBalance_RequiresReason_OnlyOnce()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1", qty: 10, cost: 100);
        var p = f.Purchases.PaySupplier(f.Supplier.Id, 400, null, f.AdminId);

        Assert.Throws<BusinessException>(() => f.Purchases.CancelPayment(p.Id, " ", f.AdminId));
        Assert.Equal(600m, Balance(f).Balance);

        f.Purchases.CancelPayment(p.Id, "Ghi nhầm", f.AdminId);
        Assert.Equal(1000m, Balance(f).Balance);

        Assert.Throws<BusinessException>(() => f.Purchases.CancelPayment(p.Id, "Lại", f.AdminId));
        Assert.Equal(1000m, Balance(f).Balance);
    }

    [Fact]
    public void History_RunningBalance_NewestFirst_KeepsCancelledPayment()
    {
        using var f = new PurchaseFixture();
        f.AdminReceipt("HD1", qty: 10, cost: 100);
        var p = f.Purchases.PaySupplier(f.Supplier.Id, 300, "Lần 1", f.AdminId);
        f.Purchases.PaySupplier(f.Supplier.Id, 200, "Lần 2", f.AdminId);
        f.Purchases.CancelPayment(p.Id, "Ghi nhầm", f.AdminId);

        var history = f.Purchases.GetSupplierHistory(f.Supplier.Id);

        Assert.Equal(3, history.Count);
        Assert.Equal(800m, history[0].BalanceAfter);
        Assert.Equal(200m, history[0].Decrease);
        var cancelled = history.Single(h => h.PaymentId == p.Id);
        Assert.True(cancelled.IsCancelled);
        Assert.Equal(0m, cancelled.Decrease);
        var receipt = history.Single(h => h.PaymentId == null);
        Assert.Equal(1000m, receipt.Increase);
        Assert.Equal(1000m, receipt.BalanceAfter);
    }

    [Fact]
    public void Supplier_WithoutTransactions_ListedAtZero()
    {
        using var f = new PurchaseFixture();
        Assert.Equal(0m, Balance(f).Balance);
    }
}
