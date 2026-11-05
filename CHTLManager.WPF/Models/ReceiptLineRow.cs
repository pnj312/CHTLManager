using System.ComponentModel;
using System.Runtime.CompilerServices;
using CHTLManager.DAL.Entities;

namespace CHTLManager.WPF.Models;

/// <summary>
/// Một dòng đang nhập trên phiếu. Số lượng và giá giữ ở dạng chữ để ô nhập sai không làm kẹt lưới;
/// chỉ kiểm tra khi lưu phiếu.
/// </summary>
public class ReceiptLineRow : INotifyPropertyChanged
{
    private string _invoiced = "1";
    private string _received = "1";
    private string _cost = string.Empty;

    public required Product Product { get; init; }
    public string ProductName => Product.Name;

    public string InvoicedText
    {
        get => _invoiced;
        set
        {
            // Thực nhận mặc định bằng số trên hóa đơn, trừ khi người dùng đã sửa khác đi.
            if (_received == _invoiced) ReceivedText = value;
            _invoiced = value;
            OnPropertyChanged();
        }
    }

    public string ReceivedText
    {
        get => _received;
        set { _received = value; OnPropertyChanged(); }
    }

    public string CostText
    {
        get => _cost;
        set { _cost = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
