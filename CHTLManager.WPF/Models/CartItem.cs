using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CHTLManager.WPF.Models;

/// <summary>Một dòng trong giỏ hàng tại màn hình bán hàng.</summary>
public class CartItem : INotifyPropertyChanged
{
    private int _quantity = 1;

    public int ProductId { get; init; }
    public string Barcode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }

    /// <summary>Tồn kho tại thời điểm tải dữ liệu, dùng để giới hạn số lượng.</summary>
    public int Stock { get; init; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            int v = Math.Max(1, Math.Min(value, Math.Max(Stock, 1)));
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
