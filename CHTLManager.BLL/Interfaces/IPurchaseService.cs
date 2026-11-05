using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

/// <summary>Một dòng khi tạo phiếu nhập. UnitCost để trống khi thu ngân nhận hàng.</summary>
public record ReceiptLineInput(int ProductId, int InvoicedQuantity, int ReceivedQuantity, decimal? UnitCost);

/// <summary>Công nợ hiện tại của một nhà cung cấp.</summary>
public record SupplierBalance(Supplier Supplier, decimal TotalPurchased, decimal TotalPaid)
{
    public decimal Balance => TotalPurchased - TotalPaid;
}

/// <summary>Một dòng trong lịch sử công nợ: phiếu nhập làm tăng nợ, lần trả làm giảm nợ.</summary>
public record SupplierLedgerEntry(DateTime Date, string Description, decimal Increase, decimal Decrease,
    decimal BalanceAfter, string UserName, int? PaymentId, bool IsCancelled);

public interface IPurchaseService
{
    List<SupplierBalance> GetSupplierBalances();

    /// <summary>Lịch sử công nợ, mới nhất trước, kèm số dư sau mỗi dòng.</summary>
    List<SupplierLedgerEntry> GetSupplierHistory(int supplierId);

    /// <summary>Ghi một lần trả tiền; không được trả quá số còn nợ.</summary>
    SupplierPayment PaySupplier(int supplierId, decimal amount, string? note, int adminId);

    /// <summary>Hủy lần trả ghi nhầm hoặc đã được hoàn tiền; bắt buộc có lý do.</summary>
    void CancelPayment(int paymentId, string reason, int adminId);

    /// <summary>Thêm nhà cung cấp mới; nếu đã có tên đó (không phân biệt hoa thường) thì trả về nhà cung cấp đã có.</summary>
    Supplier SaveSupplier(Supplier supplier);

    List<Supplier> GetSuppliers();

    /// <summary>
    /// Tạo phiếu nhập và cộng tồn kho ngay theo số thực nhận. Admin (<paramref name="asAdmin"/>) phải có giá
    /// và phiếu được xác nhận luôn; thu ngân không nhập giá, phiếu chờ Admin xác nhận.
    /// </summary>
    PurchaseReceipt CreateReceipt(int supplierId, string invoiceNo, IReadOnlyList<ReceiptLineInput> lines, int userId, bool asAdmin);

    /// <summary>Admin đối chiếu phiếu đang chờ và nhập giá cho mọi dòng.</summary>
    PurchaseReceipt ConfirmReceipt(int receiptId, IReadOnlyDictionary<int, decimal> unitCostByLineId, int adminId);

    /// <summary>Hủy phiếu, trừ lại phần đã cộng vào kho; có thể làm sản phẩm rơi vào trạng thái bán ngoài sổ.</summary>
    void CancelReceipt(int receiptId, string reason, int adminId);

    List<PurchaseReceipt> GetReceipts(DateTime from, DateTime to, ReceiptStatus? status);

    int CountPendingReceipts();
}
