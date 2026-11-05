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
    OutOfBookSale = 4
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

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public StockMovementType Type { get; set; }
    public int BeforeStock { get; set; }
    public int AfterStock { get; set; }
    public int Delta { get; set; }
    public StockReason Reason { get; set; }

    /// <summary>Giá bán tại thời điểm ghi, dùng để tính giá trị thất thoát.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Thất thoát thật: kiểm kê giảm do mất, hỏng, hết hạn (không tính đếm sai).</summary>
    [NotMapped]
    public bool IsLoss => Type == StockMovementType.StockCount && Delta < 0 &&
                          Reason is StockReason.Lost or StockReason.Damaged or StockReason.Expired;

    [NotMapped]
    public decimal LossValue => IsLoss ? -Delta * UnitPrice : 0;

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
        StockMovementType.StockCount => Delta < 0 && IsLoss ? "Kiểm kê: thất thoát" : "Kiểm kê",
        StockMovementType.OutOfBookSale => "Bán ngoài sổ",
        _ => string.Empty
    };

    [NotMapped]
    public string StockChange => $"{Product.FormatStock(BeforeStock)} → {Product.FormatStock(AfterStock)}";

    [NotMapped]
    public string DeltaText => Delta > 0 ? $"+{Delta}" : Delta.ToString();

    /// <summary>Lý do của kiểm kê, hoặc mã hóa đơn của bán ngoài sổ.</summary>
    [NotMapped]
    public string Detail => Order != null ? $"Hóa đơn {Order.Code}" : ReasonName(Reason);

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
