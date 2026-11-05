using CHTLManager.BLL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.Tests;

public class OutOfBookLimitTests
{
    private static (TestDb db, int cashierId, Product product) Setup(int stock)
    {
        var db = new TestDb().Initialized();
        var admin = db.Users.Login("admin", "admin123")!;
        var cashier = db.Users.Create("tn", "123456", "Thu ngân", UserRole.Cashier);
        var product = db.Products.Add(new Product { Barcode = "111", Name = "Coca", Price = 1000, Stock = stock }, admin.Id);
        return (db, cashier.Id, product);
    }

    private static Order Sell(TestDb db, int cashierId, Product p, int quantity)
        => db.Orders.CreateOrder(new Order { CashierId = cashierId, CustomerPaid = 100_000_000 },
            new() { new OrderDetail { ProductId = p.Id, Quantity = quantity } }, new HashSet<int> { p.Id });

    [Fact]
    public void Limit_IsTen() => Assert.Equal(10, OrderService.MaxOutOfBookPerLine);

    [Fact]
    public void OutOfBookOver10_Blocked_StockUnchanged()
    {
        var (db, cashierId, p) = Setup(0);
        using (db)
        {
            var ex = Assert.Throws<BusinessException>(() => Sell(db, cashierId, p, 11));
            Assert.Contains("10", ex.Message);
            Assert.Equal(0, db.Products.GetById(p.Id)!.Stock);
        }
    }

    [Fact]
    public void OutOfBookExactly10_Allowed()
    {
        var (db, cashierId, p) = Setup(0);
        using (db)
        {
            Sell(db, cashierId, p, 10);
            Assert.Equal(-10, db.Products.GetById(p.Id)!.Stock);
        }
    }

    [Fact]
    public void Stock5_Sell15_ExactlyTenBeyondStock_Allowed()
    {
        var (db, cashierId, p) = Setup(5);
        using (db)
        {
            Sell(db, cashierId, p, 15);
            Assert.Equal(-10, db.Products.GetById(p.Id)!.Stock);
        }
    }

    [Fact]
    public void Stock5_Sell16_Blocked()
    {
        var (db, cashierId, p) = Setup(5);
        using (db)
        {
            Assert.Throws<BusinessException>(() => Sell(db, cashierId, p, 16));
        }
    }

    [Fact]
    public void AlreadyOutOfBook_CountsOnlyThisLine()
    {
        var (db, cashierId, p) = Setup(0);
        using (db)
        {
            Sell(db, cashierId, p, 10);
            Sell(db, cashierId, p, 10);
            Assert.Equal(-20, db.Products.GetById(p.Id)!.Stock);
        }
    }
}
