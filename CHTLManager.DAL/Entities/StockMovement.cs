using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CHTLManager.DAL.Entities;

/// <summary>Loại thay đổi tồn kho được ghi nhật ký.</summary>
public enum StockMovementType
{
    /// <summary>Khai báo tồn kho khi thêm mới hoặc kinh doanh lại sản phẩm.</summary>
    Initial = 1,
    Import = 2,
    StockCount = 3,
    /// <summary>Bán khi phần mềm báo không đủ hàng (thu ngân đã xác nhận).</summary>
    OutOfBookSale = 4,
    /// <summary>Nhập hàng theo phiếu nhập.</summary>
    ReceiptIn = 5,
    /// <summary>Hủy phiếu nhập, trừ lại phần đã cộng.</summary>
    ReceiptCancelled = 6,
    /// <summary>Khai báo giá vốn cho sản phẩm chưa có giá; không đổi tồn kho.</summary>
    CostDeclared = 7,
    /// <summary>Hủy hóa đơn bán, hoàn lại số đã trừ.</summary>
    SaleCancelled = 8,
    /// <summary>Sửa giá nhập của phiếu đã xác nhận; không đổi tồn kho.</summary>
    CostCorrected = 9
}

/// <summary>Lý do chênh lệch khi kiểm kê.</summary>
public enum StockReason
{
    None = 0,
    Lost = 1,
    Damaged = 2,
    Expired = 3,
    MiscountBefore = 4,
    UnrecordedImport = 5
}

/// <summary>Một dòng nhật ký kho: ai đổi tồn kho, lúc nào, từ bao nhiêu thành bao nhiêu, vì sao.</summary>
public class StockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Hóa đơn liên quan (chỉ có với bán ngoài sổ).</summary>
    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>Phiếu nhập liên quan (nhập hoặc hủy phiếu).</summary>
    public int? PurchaseReceiptId { get; set; }
    public PurchaseReceipt? PurchaseReceipt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public StockMovementType Type { get; set; }
    public int BeforeStock { get; set; }
    public int AfterStock { get; set; }
    public int Delta { get; set; }
    public StockReason Reason { get; set; }

    /// <summary>Giá bán tại thời điểm ghi, dùng để tính giá trị thất thoát.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Giá vốn được khai báo (chỉ có ở dòng khai báo giá vốn).</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>Nhà cung cấp của lô nhập.</summary>
    [MaxLength(100)]
    public string? Supplier { get; set; }

    /// <summary>Số hóa đơn của nhà cung cấp, để đối chiếu từng lô nhập.</summary>
    [MaxLength(50)]
    public string? InvoiceNo { get; set; }

    /// <summary>Ghi chú của thao tác, ví dụ giá cũ, giá mới và lý do sửa giá nhập.</summary>
    [MaxLength(300)]
    public string? Note { get; set; }

    /// <summary>Số cái bán vượt số đã ghi trong sổ ở lần này.</summary>
    [NotMapped]
    public int OutOfBookQuantity => Type == StockMovementType.OutOfBookSale
        ? Math.Max(0, -AfterStock) - Math.Max(0, -BeforeStock)
        : 0;

    [NotMapped]
    public string TypeName => Type switch
    {
        StockMovementType.Initial => "Khai báo ban đầu",
        StockMovementType.Import => "Nhập kho",
        StockMovementType.StockCount => "Kiểm kê",
        StockMovementType.OutOfBookSale => "Bán ngoài sổ",
        StockMovementType.ReceiptIn => "Nhập theo phiếu",
        StockMovementType.ReceiptCancelled => "Hủy phiếu nhập",
        StockMovementType.CostDeclared => "Khai báo giá vốn",
        StockMovementType.SaleCancelled => "Hủy hóa đơn bán",
        StockMovementType.CostCorrected => "Sửa giá nhập",
        _ => string.Empty
    };

    [NotMapped]
    public string StockChange => $"{Product.FormatStock(BeforeStock)} → {Product.FormatStock(AfterStock)}";

    [NotMapped]
    public string DeltaText => Delta > 0 ? $"+{Delta}" : Delta.ToString();

    /// <summary>Hóa đơn bán (bán ngoài sổ), nhà cung cấp và số hóa đơn (nhập kho), hoặc lý do (kiểm kê).</summary>
    [NotMapped]
    public string Detail
    {
        get
        {
            if (Order != null) return $"Hóa đơn bán {Order.Code}";
            if (Type == StockMovementType.CostDeclared) return $"Giá vốn {UnitCost:N0}";

            var parts = new[]
            {
                Note ?? string.Empty,
                ReasonName(Reason),
                Supplier ?? string.Empty,
                string.IsNullOrEmpty(InvoiceNo) ? string.Empty : $"HĐ {InvoiceNo}"
            };
            return string.Join(", ", parts.Where(p => p.Length > 0));
        }
    }

    public static string ReasonName(StockReason reason) => reason switch
    {
        StockReason.Lost => "Mất",
        StockReason.Damaged => "Hỏng",
        StockReason.Expired => "Hết hạn",
        StockReason.MiscountBefore => "Đếm sai trước đó",
        StockReason.UnrecordedImport => "Hàng nhập chưa ghi",
        _ => string.Empty
    };
}
