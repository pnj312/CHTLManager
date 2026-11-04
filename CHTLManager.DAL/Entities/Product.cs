using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    /// <summary>Số lượng tồn kho trên phần mềm. Có thể âm khi đã bán vượt tồn (kho lệch thực tế, cần kiểm kê).</summary>
    public int Stock { get; set; }

    /// <summary>True khi tồn kho âm: số trên phần mềm thấp hơn thực tế, cần kiểm kê.</summary>
    [NotMapped]
    public bool NeedsStockCheck => Stock < 0;

    /// <summary>False = đã ngừng kinh doanh (xóa mềm khi sản phẩm đã có trong hóa đơn).</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}
