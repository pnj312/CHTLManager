using CHTLManager.BLL;
using CHTLManager.DAL;
using CHTLManager.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.Tests;

public class DemoSeedTests
{
    [Fact]
    public void DemoSeed_Reconciles()
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();

        var movements = db.StockMovements.AsNoTracking().ToList();
        var sold = db.OrderDetails.AsNoTracking().Include(d => d.Order).Where(d => !d.Order!.IsCancelled).ToList();

        foreach (var product in db.Products.AsNoTracking().ToList())
        {
            // Bán ngoài sổ đã nằm trong số bán, và hủy hóa đơn thì không tính hóa đơn đó nữa.
            int fromLog = movements
                .Where(m => m.ProductId == product.Id && m.Type is not (StockMovementType.OutOfBookSale or StockMovementType.SaleCancelled))
                .Sum(m => m.Delta);
            int soldQuantity = sold.Where(d => d.ProductId == product.Id).Sum(d => d.Quantity);

            Assert.Equal(fromLog - soldQuantity, product.Stock);
            Assert.True(product.Stock >= 0, product.Name);
            Assert.True(product.AverageCost > 0, product.Name);
        }
    }

    [Fact]
    public void DemoSeed_MovementsFormAChainForEachProduct()
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();

        // Giữa hai dòng nhật ký chỉ có bán thường, nên tồn kho chỉ có thể giảm.
        foreach (var group in db.StockMovements.AsNoTracking().ToList().GroupBy(m => m.ProductId))
        {
            var ordered = group.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
            Assert.Equal(StockMovementType.Initial, ordered[0].Type);
            for (int i = 1; i < ordered.Count; i++)
                Assert.True(ordered[i].BeforeStock <= ordered[i - 1].AfterStock, $"{group.Key}: dòng {i}");
        }
    }

    [Fact]
    public void DemoSeed_DebtAndProfit_MatchDocuments()
    {
        using var t = new TestDb().Initialized(demo: true);
        using var db = new AppDbContext();

        var receipts = db.PurchaseReceipts.AsNoTracking().Include(r => r.Lines).ToList();
        decimal purchased = receipts.Where(r => r.Status == ReceiptStatus.Confirmed).Sum(r => r.Total);
        decimal paid = db.SupplierPayments.AsNoTracking().ToList().Sum(p => p.Amount);

        var balances = new PurchaseService().GetSupplierBalances();
        Assert.True(purchased > 0);
        Assert.True(paid > 0);
        Assert.Equal(purchased - paid, balances.Sum(b => b.Balance));
        Assert.All(balances, b => Assert.True(b.Balance >= 0));

        var orders = db.Orders.AsNoTracking().ToList();
        var details = db.OrderDetails.AsNoTracking().ToList();
        var report = new OrderService().GetProfitReport(DateTime.Today.AddDays(-7), DateTime.Today);
        Assert.Equal(orders.Sum(o => o.Total), report.Revenue);
        Assert.Equal(details.Sum(d => d.UnitCost * d.Quantity), report.CostOfGoods);
        Assert.Equal(0, report.EstimatedLineCount);
        Assert.True(report.LossExpense > 0);
        Assert.True(report.CompensationCollected > 0);
    }

    [Fact]
    public void DemoSeed_NothingWaitingForAdmin()
    {
        using var t = new TestDb().Initialized(demo: true);

        Assert.Equal(0, new PurchaseService().CountPendingReceipts());
        Assert.Equal(0, new StockCountService().CountPending());
    }
}
