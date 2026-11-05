using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CHTLManager.DAL.Entities;

/// <summary>Nhà cung cấp hàng cho cửa hàng.</summary>
public class Supplier
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Tên chuẩn hóa để không tạo trùng khi khác hoa thường, khoảng trắng.</summary>
    [Required, MaxLength(100)]
    public string NameKey { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Một lần trả tiền cho nhà cung cấp. Ghi nhầm thì hủy kèm lý do, không xóa.</summary>
public class SupplierPayment
{
    public int Id { get; set; }

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public bool IsCancelled { get; set; }

    [MaxLength(200)]
    public string? CancelReason { get; set; }
}

public enum ReceiptStatus
{
    /// <summary>Thu ngân đã nhận hàng, chờ Admin đối chiếu và nhập giá. Tồn kho đã cộng.</summary>
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3
}

/// <summary>Phiếu nhập: một nhà cung cấp, một số hóa đơn, nhiều dòng sản phẩm.</summary>
public class PurchaseReceipt
{
    public int Id { get; set; }

    /// <summary>Số phiếu, ví dụ PN20261105-0001.</summary>
    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Số hóa đơn của nhà cung cấp.</summary>
    [Required, MaxLength(50)]
    public string InvoiceNo { get; set; } = string.Empty;

    /// <summary>Số hóa đơn chuẩn hóa, dùng để chặn nhập trùng.</summary>
    [Required, MaxLength(50)]
    public string InvoiceKey { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public int? ConfirmedByUserId { get; set; }
    public User? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public ReceiptStatus Status { get; set; }

    [MaxLength(200)]
    public string? CancelReason { get; set; }

    /// <summary>Phiếu sinh ra từ kiểm kê (lô chưa ghi): hàng đã nằm trong tồn kho từ lúc đếm.</summary>
    public int? StockDiscrepancyId { get; set; }
    public StockDiscrepancy? StockDiscrepancy { get; set; }

    public List<PurchaseReceiptLine> Lines { get; set; } = new();

    /// <summary>Tiền hàng theo số thực nhận; chỉ đủ khi mọi dòng đã có giá.</summary>
    [NotMapped]
    public decimal Total => Lines.Sum(l => l.LineTotal);

    [NotMapped]
    public string StatusName => Status switch
    {
        ReceiptStatus.Pending => "Chờ xác nhận",
        ReceiptStatus.Confirmed => "Đã xác nhận",
        ReceiptStatus.Cancelled => "Đã hủy",
        _ => string.Empty
    };

    [NotMapped]
    public string TotalDisplay => Status == ReceiptStatus.Pending ? "Chờ nhập giá" : Total.ToString("N0");
}

public class PurchaseReceiptLine
{
    public int Id { get; set; }

    public int PurchaseReceiptId { get; set; }
    public PurchaseReceipt? PurchaseReceipt { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int InvoicedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }

    /// <summary>Giá nhập một đơn vị; null khi thu ngân nhận hàng và Admin chưa xác nhận.</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>
    /// Số hàng có giá đã có lúc giá của dòng này vào bình quân, để sau này hủy hoặc sửa giá tính lại được.
    /// Null ở dữ liệu cũ; 0 khi trước đó chưa biết giá.
    /// </summary>
    public int? CostBaseStock { get; set; }

    [NotMapped]
    public decimal LineTotal => (UnitCost ?? 0) * ReceivedQuantity;

    /// <summary>Số nhận thiếu so với hóa đơn; âm nghĩa là nhận thừa. Là việc với nhà cung cấp, không phải thất thoát.</summary>
    [NotMapped]
    public int ShortQuantity => InvoicedQuantity - ReceivedQuantity;
}
