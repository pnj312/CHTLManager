using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

/// <summary>Một dòng khi tạo phiếu nhập. UnitCost để trống khi thu ngân nhận hàng.</summary>
public record ReceiptLineInput(int ProductId, int InvoicedQuantity, int ReceivedQuantity, decimal? UnitCost);

public interface IPurchaseService
{
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
