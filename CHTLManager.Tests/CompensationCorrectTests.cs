using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public class CompensationCorrectTests
{
    private static StockCountService Counts => new();

    /// <summary>Oreo tồn 5, giá vốn 12.000, đếm còn 3; thu ngân phải bồi thường 21.000.</summary>
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
    public void Amount_UpdatesRemaining_StockAndLossUnchanged()
    {
        var (f, d) = Setup();
        using var _ = f;
        decimal lossBefore = f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).LossExpense;

        Counts.CorrectCompensation(d.Id, f.CashierId, null, 12000, "Gõ nhầm số tiền", f.AdminId);

        Assert.Equal(12000m, Reload(d.Id).CompensationRemaining);
        Assert.Equal(3, f.Stock(f.Oreo));
        Assert.Equal(lossBefore, f.Db.Orders.GetProfitReport(DateTime.Today, DateTime.Today).LossExpense);
    }

    [Fact]
    public void Person_ToOtherAccount()
    {
        var (f, d) = Setup();
        using var _ = f;
        var other = f.Db.Users.Create("tn2", "123456", "Thu ngân 2", UserRole.Cashier);

        Counts.CorrectCompensation(d.Id, other.Id, null, 21000, "Ghi nhầm người", f.AdminId);

        var saved = Reload(d.Id);
        Assert.Equal(other.Id, saved.ResponsibleUserId);
        Assert.Equal("Thu ngân 2", saved.ResponsibleDisplay);
    }

    [Fact]
    public void Person_ToTypedName_ClearsAccount()
    {
        var (f, d) = Setup();
        using var _ = f;

        Counts.CorrectCompensation(d.Id, null, "  Bác bảo vệ ", 21000, "Không phải thu ngân", f.AdminId);

        var saved = Reload(d.Id);
        Assert.Null(saved.ResponsibleUserId);
        Assert.Equal("Bác bảo vệ", saved.ResponsibleDisplay);
    }

    [Fact]
    public void IsLogged_WithOldNewAndReason()
    {
        var (f, d) = Setup();
        using var _ = f;
        var other = f.Db.Users.Create("tn2", "123456", "Thu ngân 2", UserRole.Cashier);

        Counts.CorrectCompensation(d.Id, other.Id, null, 12000, "Ghi nhầm", f.AdminId);

        var m = f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements[0];
        Assert.Equal(StockMovementType.CompensationCorrected, m.Type);
        Assert.Equal(0, m.Delta);
        Assert.Equal(f.AdminId, m.UserId);
        Assert.Equal(f.Oreo.Id, m.ProductId);
        Assert.Contains("Thu ngân", m.Detail);
        Assert.Contains("Thu ngân 2", m.Detail);
        Assert.Contains("21.000", m.Detail);
        Assert.Contains("12.000", m.Detail);
        Assert.Contains("Ghi nhầm", m.Detail);
    }

    [Fact]
    public void Person_WithActiveCollection_Blocked()
    {
        var (f, d) = Setup();
        using var _ = f;
        var other = f.Db.Users.Create("tn2", "123456", "Thu ngân 2", UserRole.Cashier);
        Counts.CollectCompensation(d.Id, 5000, null, f.AdminId);

        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, other.Id, null, 21000, "Ghi nhầm", f.AdminId));

        Assert.Equal(f.CashierId, Reload(d.Id).ResponsibleUserId);
    }

    [Fact]
    public void Person_AfterCollectionCancelled_Allowed()
    {
        var (f, d) = Setup();
        using var _ = f;
        var other = f.Db.Users.Create("tn2", "123456", "Thu ngân 2", UserRole.Cashier);
        var p = Counts.CollectCompensation(d.Id, 5000, null, f.AdminId);
        Counts.CancelCompensationPayment(p.Id, "Thu nhầm người", f.AdminId);

        Counts.CorrectCompensation(d.Id, other.Id, null, 21000, "Ghi nhầm", f.AdminId);

        Assert.Equal(other.Id, Reload(d.Id).ResponsibleUserId);
    }

    [Fact]
    public void Amount_BelowCollected_Blocked_EqualAllowed()
    {
        var (f, d) = Setup();
        using var _ = f;
        Counts.CollectCompensation(d.Id, 5000, null, f.AdminId);

        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, f.CashierId, null, 4999, "Giảm", f.AdminId));
        Assert.Equal(16000m, Reload(d.Id).CompensationRemaining);

        Counts.CorrectCompensation(d.Id, f.CashierId, null, 5000, "Giảm", f.AdminId);
        Assert.Equal(0m, Reload(d.Id).CompensationRemaining);
    }

    [Fact]
    public void Invalid_Blocked_NothingChanged()
    {
        var (f, d) = Setup();
        using var _ = f;

        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, f.CashierId, null, 12000, " ", f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, f.CashierId, null, 0, "x", f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, f.CashierId, null, -1, "x", f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, null, "  ", 12000, "x", f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, 99999, null, 12000, "x", f.AdminId));
        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(99999, f.CashierId, null, 12000, "x", f.AdminId));

        Assert.Equal(21000m, Reload(d.Id).CompensationAmount);
    }

    [Fact]
    public void NothingChanged_Blocked_NotLogged()
    {
        var (f, d) = Setup();
        using var _ = f;
        int logBefore = f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements.Count;

        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, f.CashierId, null, 21000, "Không đổi", f.AdminId));

        Assert.Equal(logBefore, f.Db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NotCompensation_Blocked(bool pending)
    {
        var f = new PurchaseFixture();
        using var _ = f;
        f.Db.Products.SetOpeningCost(f.Oreo.Id, 12000, f.AdminId);
        var d = Counts.Count(f.Oreo.Id, 3, f.AdminId)!;
        if (!pending) Counts.ResolveShortage(d.Id, DiscrepancyResolution.NormalLoss, null, null, null, null, f.AdminId);

        Assert.Throws<BusinessException>(() => Counts.CorrectCompensation(d.Id, f.CashierId, null, 12000, "x", f.AdminId));

        Assert.Null(Reload(d.Id).CompensationAmount);
    }
}
