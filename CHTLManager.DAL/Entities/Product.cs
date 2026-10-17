using System.ComponentModel.DataAnnotations;

namespace CHTLManager.DAL.Entities;

/// <summary>Sản phẩm bán tại cửa hàng tiện lợi.</summary>
public class Product
{
    public int Id { get; set; }

    /// <summary>Mã vạch (duy nhất).</summary>
    [Required, MaxLength(50)]
    public string Barcode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Giá bán (VNĐ).</summary>
    public decimal Price { get; set; }

    /// <summary>Số lượng tồn kho.</summary>
    public int Stock { get; set; }

    /// <summary>False = đã ngừng kinh doanh (xóa mềm khi sản phẩm đã có trong hóa đơn).</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
