using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public class ProductLifecycleTests
{
    private static (TestDb db, int adminId, int cashierId, Product product) Setup(int stock)
    {
        var db = new TestDb().Initialized();
        var admin = db.Users.Login("admin", "admin123")!;
        var cashier = db.Users.Create("tn", "123456", "Thu ngân", UserRole.Cashier);
        var product = db.Products.Add(new Product { Barcode = "111", Name = "Coca", Price = 10000, Stock = stock }, admin.Id);
        return (db, admin.Id, cashier.Id, product);
    }

    [Fact]
    public void Delete_WithPositiveStock_Blocked()
    {
        var (db, _, _, p) = Setup(5);
        using (db)
        {
            var ex = Assert.Throws<BusinessException>(() => db.Products.Delete(p.Id));
            Assert.Contains("kiểm kê", ex.Message);
            Assert.NotNull(db.Products.GetById(p.Id));
        }
    }

    [Fact]
    public void Delete_WithOutOfBookStock_Blocked()
    {
        var (db, _, cashierId, p) = Setup(0);
        using (db)
        {
            db.Orders.CreateOrder(new Order { CashierId = cashierId, CustomerPaid = 100000 },
                new() { new OrderDetail { ProductId = p.Id, Quantity = 2 } }, new HashSet<int> { p.Id });

            Assert.Throws<BusinessException>(() => db.Products.Delete(p.Id));
            Assert.Equal(-2, db.Products.GetById(p.Id)!.Stock);
        }
    }

    [Fact]
    public void Delete_ZeroStockWithHistory_SoftDeletes()
    {
        var (db, adminId, _, p) = Setup(5);
        using (db)
        {
            db.Products.SetCountedStock(p.Id, 0, StockReason.Lost, adminId);

            Assert.False(db.Products.Delete(p.Id));
            Assert.Null(db.Products.GetById(p.Id));
        }
    }

    [Fact]
    public void Add_ReactivatedProduct_IgnoresFormStock()
    {
        var (db, adminId, _, p) = Setup(5);
        using (db)
        {
            db.Products.SetCountedStock(p.Id, 0, StockReason.Lost, adminId);
            db.Products.Delete(p.Id);
            int movements = db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements.Count;

            var again = db.Products.Add(new Product { Barcode = "111", Name = "Coca mới", Price = 12000, Stock = 50 }, adminId);

            Assert.Equal(p.Id, again.Id);
            Assert.Equal(0, again.Stock);
            Assert.Equal("Coca mới", again.Name);
            Assert.Equal(movements, db.Products.GetStockLog(DateTime.Today, DateTime.Today).Movements.Count);
        }
    }
}
