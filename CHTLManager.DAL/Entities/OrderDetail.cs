using System.ComponentModel.DataAnnotations.Schema;

namespace CHTLManager.DAL.Entities;

/// <summary>Dòng chi tiết của hóa đơn (một sản phẩm, một số lượng).</summary>
public class OrderDetail
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int Quantity { get; set; }

    /// <summary>Đơn giá tại thời điểm bán (không đổi khi giá sản phẩm thay đổi sau này).</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Giá vốn một đơn vị tại thời điểm bán; không tính lại khi nhập lô sau.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Giá vốn tạm tính: bán ngoài sổ, hoặc sản phẩm chưa có giá vốn lúc bán.</summary>
    public bool CostIsEstimated { get; set; }

    [NotMapped]
    public decimal LineTotal => UnitPrice * Quantity;

    [NotMapped]
    public decimal LineCost => UnitCost * Quantity;
}
