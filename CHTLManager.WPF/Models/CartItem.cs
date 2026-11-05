using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CHTLManager.WPF.Models;

/// <summary>Một dòng trong giỏ hàng tại màn hình bán hàng.</summary>
public class CartItem : INotifyPropertyChanged
{
    private int _quantity = 1;
    private bool _oversellConfirmed;

    public int ProductId { get; init; }
    public string Barcode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }

    /// <summary>Tồn kho tại thời điểm tải dữ liệu, dùng để giới hạn số lượng.</summary>
    public int Stock { get; init; }

    /// <summary>
    /// Thu ngân đã xác nhận hàng thực tế vẫn còn dù kho báo không đủ: được bán vượt tồn kho.
    /// </summary>
    public bool OversellConfirmed
    {
        get => _oversellConfirmed;
        set
        {
            _oversellConfirmed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>Tên hiển thị trong giỏ, đánh dấu dòng bán ngoài sổ.</summary>
    public string DisplayName => OversellConfirmed ? $"{Name} (kho chưa ghi)" : Name;

    public int Quantity
    {
        get => _quantity;
        set
        {
            int v = Math.Max(1, value);
            if (!OversellConfirmed) v = Math.Min(v, Math.Max(Stock, 1));
            _quantity = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineTotal));
        }
    }

    public decimal LineTotal => UnitPrice * Quantity;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
