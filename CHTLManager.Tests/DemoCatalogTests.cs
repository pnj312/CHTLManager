using CHTLManager.BLL;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.Tests;

/// <summary>Dữ liệu demo cố định từng sản phẩm. Số kỳ vọng tính tay từ kịch bản nhập, bán, kiểm kê.</summary>
public class DemoCatalogTests
{
    private const string HaoHao = "8934563138165", Omachi = "8934563138172", Aquafina = "8935049500018", Coca = "8934588012105",
        TraXanh = "8935024140013", Sua = "8934673101011", Oreo = "8934804023012", Oishi = "8935001710015",
        G7 = "8936017360018", DauGoi = "8934868150012", KhanGiay = "8935210600019", But = "8936079120011";

    private static Product Get(AppDbContext db, string barcode) => db.Products.AsNoTracking().Single(p => p.Barcode == barcode);

    [Theory]
    [InlineData(HaoHao, 130)]
    [InlineData(Omachi, 54)]
    [InlineData(Aquafina, 107)]
    [InlineData(Coca, 130)]
    [InlineData(TraXanh, 66)]
    [InlineData(Sua, 77)]
    [InlineData(Oreo, 34)]
    [InlineData(Oishi, 68)]
    [InlineData(G7, 22)]
    [InlineData(DauGoi, 5)]
    [InlineData(KhanGiay, 45)]
    [InlineData(But, 195)]
    public void EveryProduct_HasTheScriptedStock(string barcode, int stock)
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();

        Assert.Equal(stock, Get(db, barcode).Stock);
    }

    [Theory]
    [InlineData(HaoHao, 3434.78)]  // (90×3.400 + 48×3.500) / 138
    [InlineData(Omachi, 5600)]
    [InlineData(Aquafina, 3800)]
    [InlineData(Coca, 7067.61)]    // (94×7.000 + 48×7.200) / 142
    [InlineData(TraXanh, 6800)]
    [InlineData(Sua, 6500)]
    [InlineData(Oreo, 11500)]
    [InlineData(Oishi, 5056.34)]   // (51×5.000 + 20×5.200) / 71
    [InlineData(G7, 40000)]
    [InlineData(DauGoi, 72000)]
    [InlineData(KhanGiay, 8500)]
    [InlineData(But, 2500)]
    public void EveryProduct_HasItsOwnCost(string barcode, double cost)
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();

        Assert.Equal((decimal)cost, Get(db, barcode).AverageCost);
    }

    [Fact]
    public void EveryProduct_ComesFromAReceipt_NoStockWithoutDocument()
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();

        Assert.DoesNotContain(db.StockMovements.AsNoTracking().ToList(), m => m.Type == StockMovementType.Initial);
        var received = db.PurchaseReceiptLines.AsNoTracking().Select(l => l.ProductId).Distinct().ToList();
        Assert.All(db.Products.AsNoTracking().ToList(), p => Assert.Contains(p.Id, received));
        Assert.All(db.PurchaseReceiptLines.AsNoTracking().ToList(), l => Assert.NotNull(l.CostBaseStock));
    }

    [Fact]
    public void Debts_ByScriptedSupplier()
    {
        using var t = new TestDb().Initialized(demo: true);

        var balances = new PurchaseService().GetSupplierBalances().ToDictionary(b => b.Supplier.Name, b => b.Balance);

        Assert.Equal(4, balances.Count);
        Assert.Equal(963200m, balances["NPP Đồ uống"]);
        Assert.Equal(2768000m, balances["NPP Thực phẩm"]);
        Assert.Equal(925000m, balances["NPP Tạp hóa"]);
        Assert.Equal(0m, balances["NPP Unilever"]);
    }

    [Fact]
    public void Sales_TotalAndOrderCount_AreFixed()
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();

        Assert.Equal(25, db.Orders.Count());
        Assert.Equal(1300000m, new OrderService().GetProfitReport(DateTime.Today.AddDays(-7), DateTime.Today).Revenue);
    }

    [Fact]
    public void Compensation_MatchesTheLostValue()
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();

        var d = db.StockDiscrepancies.AsNoTracking().Include(x => x.Payments).Single(x => x.Resolution == DiscrepancyResolution.Compensation);

        Assert.Equal(17000m, d.CompensationAmount);
        Assert.Equal(17000m, d.Value);
        Assert.Equal(7000m, d.CompensationRemaining);
    }

    [Fact]
    public void CancellingADemoLot_RecalculatesCorrectly()
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();
        var coca = Get(db, Coca);
        var receipt = db.PurchaseReceipts.AsNoTracking().Single(r => r.InvoiceNo == "ĐU-1002");
        var admin = db.Users.AsNoTracking().First(u => u.Role == UserRole.Admin);

        new PurchaseService().CancelReceipt(receipt.Id, "Thử", admin.Id);

        using var after = new AppDbContext();
        // Bình quân lưu làm tròn 2 chữ số nên lùi lại có thể lệch 0,01 đ.
        Assert.InRange(Get(after, Coca).AverageCost, 6999.99m, 7000.01m);
        Assert.Equal(coca.Stock - 48, Get(after, Coca).Stock);
    }

    [Theory]
    [InlineData(0, 0, 5)]
    [InlineData(6, 30, 0)]
    [InlineData(9, 0, 0)]
    [InlineData(23, 30, 0)]
    public void AnyTimeOfDay_SameNumbers_NothingInTheFuture(int hour, int minute, int second)
    {
        using var t = new TestDb();
        DatabaseUpgrader.Upgrade();
        DateTime now = DateTime.Today.AddHours(hour).AddMinutes(minute).AddSeconds(second);

        DbInitializer.Initialize(true, now);

        using var db = new AppDbContext();
        Assert.Equal(5, Get(db, DauGoi).Stock);
        Assert.Equal(25, db.Orders.Count());
        var orders = db.Orders.AsNoTracking().ToList();
        Assert.All(orders, o => Assert.True(o.CreatedAt <= now, o.Code));
        Assert.Equal(2, orders.Count(o => o.CreatedAt >= now.Date));
    }

    [Fact]
    public void NextSaleToday_ContinuesTheCode()
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();
        var cashier = db.Users.AsNoTracking().First(u => u.Role == UserRole.Cashier);

        var order = new OrderService().CreateOrder(new Order { CashierId = cashier.Id, CustomerPaid = 100000 },
            new() { new OrderDetail { ProductId = Get(db, Coca).Id, Quantity = 1 } });

        Assert.EndsWith("-0003", order.Code);
    }
}

public class SimilarSupplierTests
{
    [Theory]
    [InlineData("NPP Coca-Cola", "NPP Coca Cola")]
    [InlineData("NPP Coca-Cola", "npp cocacola")]
    [InlineData("NPP Đồ uống", "NPP Do uong")]
    [InlineData("NPP Unilever", "NPP Unilevr")]
    public void SameSupplierTypedDifferently_IsFound(string existing, string typed)
    {
        using var f = new PurchaseFixture();
        var saved = f.Purchases.SaveSupplier(new Supplier { Name = existing });

        Assert.Equal(saved.Id, f.Purchases.FindSimilarSupplier(typed)?.Id);
    }

    [Theory]
    [InlineData("NPP A")]             // đúng tên đã có: dùng luôn, không cần hỏi
    [InlineData("  npp a ")]
    [InlineData("NPP B")]             // tên ngắn khác một ký tự là nhà cung cấp khác
    [InlineData("Công ty Thành Đạt")]
    [InlineData("")]
    [InlineData("   ")]
    public void DifferentOrExact_NotReported(string typed)
    {
        using var f = new PurchaseFixture(); // đã có "NPP A"

        Assert.Null(f.Purchases.FindSimilarSupplier(typed));
    }
}
