using System.ComponentModel.DataAnnotations;

namespace CHTLManager.DAL.Entities;

/// <summary>Hóa đơn bán hàng.</summary>
public class Order
{
    public int Id { get; set; }

    /// <summary>Số hóa đơn, ví dụ HD20261017-0001.</summary>
    [Required, MaxLength(30)]
    public string Code { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public int CashierId { get; set; }
    public User? Cashier { get; set; }

    /// <summary>Tổng tiền hàng.</summary>
    public decimal Total { get; set; }

    /// <summary>Tiền khách đưa.</summary>
    public decimal CustomerPaid { get; set; }

    /// <summary>Tiền thừa trả khách = CustomerPaid - Total.</summary>
    public decimal Change { get; set; }

    /// <summary>Đã hủy: giữ lại để đối chiếu nhưng không tính doanh thu, lợi nhuận.</summary>
    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserId { get; set; }
    public User? CancelledBy { get; set; }

    [MaxLength(200)]
    public string? CancelReason { get; set; }

    public List<OrderDetail> Details { get; set; } = new();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string StatusName => IsCancelled ? "Đã hủy" : string.Empty;
}
