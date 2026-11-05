using CHTLManager.DAL;
using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL;

/// <summary>Đổi tồn kho kèm một dòng nhật ký; người gọi lưu cả hai trong cùng một SaveChanges.</summary>
internal static class StockLedger
{
    public static void Apply(AppDbContext db, Product product, int newStock, StockMovementType type, int userId,
        StockReason reason = StockReason.None, bool logUnchanged = false, string? supplier = null,
        string? invoiceNo = null, PurchaseReceipt? receipt = null)
    {
        int before = product.Stock;
        product.Stock = newStock;
        if (before == newStock && !logUnchanged) return;

        db.StockMovements.Add(new StockMovement
        {
            Product = product,
            UserId = userId,
            CreatedAt = DateTime.Now,
            Type = type,
            BeforeStock = before,
            AfterStock = newStock,
            Delta = newStock - before,
            Reason = reason,
            UnitPrice = product.Price,
            Supplier = supplier,
            InvoiceNo = invoiceNo,
            PurchaseReceipt = receipt
        });
    }
}
