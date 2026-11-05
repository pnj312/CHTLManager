using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public class CompensationCancelTests
{
    private static StockCountService Counts => new();

    /// <summary>Thiếu 2 Oreo, phải bồi thường 21.000.</summary>
    private static (PurchaseFixture F, StockDiscrepancy D) Setup()
    {
        var f = new PurchaseFixture();
        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId);
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        Counts.ResolveShortage(d.Id, DiscrepancyResolution.Compensation, null, f.CashierId, null, 21000, f.AdminId);
        return (f, d);
    }

    private static StockDiscrepancy Reload(int id)
        => Counts.GetDiscrepancies(DateTime.Today, DateTime.Today, null).Single(d => d.Id == id);

    [Fact]
    public void Cancel_RestoresRemaining()
    {
        var (f, d) = Setup();
        using var _ = f;
        var typo = Counts.CollectCompensation(d.Id, 15000, null, f.AdminId);

        Counts.CancelCompensationPayment(typo.Id, "Gõ nhầm, thực thu 5.000", f.AdminId);

        Assert.Equal(21000m, Reload(d.Id).CompensationRemaining);
        Assert.Equal(0m, Reload(d.Id).CompensationCollected);
    }

    [Fact]
    public void Cancel_RemovedFromProfitReport()
    {
        var (f, d) = Setup();
        using var _ = f;
        var typo = Counts.CollectCompensation(d.Id, 15000, null, f.AdminId);
        Counts.CollectCompensation(d.Id, 1000, null, f.AdminId);

        Counts.CancelCompensationPayment(typo.Id, "Gõ nhầm", f.AdminId);

        Assert.Equal(1000m, f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).CompensationCollected);
    }

    [Fact]
    public void Cancel_ThenCollectCorrectAmount()
    {
        var (f, d) = Setup();
        using var _ = f;
        var fullTypo = Counts.CollectCompensation(d.Id, 21000, null, f.AdminId);
        Assert.Throws<BusinessException>(() => Counts.CollectCompensation(d.Id, 1000, null, f.AdminId));

        Counts.CancelCompensationPayment(fullTypo.Id, "Gõ nhầm", f.AdminId);
        Counts.CollectCompensation(d.Id, 5000, null, f.AdminId);

        Assert.Equal(16000m, Reload(d.Id).CompensationRemaining);
    }

    [Fact]
    public void Cancel_KeepsTheRow_WithWhoAndWhy()
    {
        var (f, d) = Setup();
        using var _ = f;
        var first = Counts.CollectCompensation(d.Id, 15000, null, f.AdminId);
        var second = Counts.CollectCompensation(d.Id, 1000, null, f.AdminId);

        Counts.CancelCompensationPayment(first.Id, "  Gõ nhầm ", f.AdminId);

        var rows = Counts.GetCompensationPayments(d.Id);
        Assert.Equal(new[] { second.Id, first.Id }, rows.Select(p => p.Id));
        Assert.False(rows[0].IsCancelled);
        Assert.True(rows[1].IsCancelled);
        Assert.Equal("Gõ nhầm", rows[1].CancelReason);
        Assert.Equal(f.AdminId, rows[1].CancelledByUserId);
        Assert.NotNull(rows[1].CancelledAt);
        Assert.Equal(15000m, rows[1].Amount);
        Assert.Contains("Gõ nhầm", rows[1].StatusText);
        Assert.Equal(f.AdminId, rows[1].User!.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_WithoutReason_Blocked(string reason)
    {
        var (f, d) = Setup();
        using var _ = f;
        var p = Counts.CollectCompensation(d.Id, 5000, null, f.AdminId);

        Assert.Throws<BusinessException>(() => Counts.CancelCompensationPayment(p.Id, reason, f.AdminId));

        Assert.Equal(16000m, Reload(d.Id).CompensationRemaining);
    }

    [Fact]
    public void Cancel_Twice_Or_Unknown_Blocked()
    {
        var (f, d) = Setup();
        using var _ = f;
        var p = Counts.CollectCompensation(d.Id, 5000, null, f.AdminId);
        Counts.CancelCompensationPayment(p.Id, "Gõ nhầm", f.AdminId);

        Assert.Throws<BusinessException>(() => Counts.CancelCompensationPayment(p.Id, "Lần nữa", f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.CancelCompensationPayment(99999, "Không có", f.AdminId));
    }

    [Fact]
    public void NewCollection_IsActive()
    {
        var (f, d) = Setup();
        using var _ = f;
        Counts.CollectCompensation(d.Id, 5000, null, f.AdminId);

        Assert.False(Counts.GetCompensationPayments(d.Id).Single().IsCancelled);
        Assert.Equal(string.Empty, Counts.GetCompensationPayments(d.Id).Single().StatusText);
    }
}
