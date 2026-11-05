using CHTLManager.DAL.Entities;

namespace CHTLManager.BLL.Interfaces;

public interface IStockCountService
{
    /// <summary>
    /// Ghi số đếm thực tế: tồn kho đổi ngay và có nhật ký. Có chênh lệch thì trả về phiếu chờ Admin xử lý, khớp thì trả về null.
    /// </summary>
    StockDiscrepancy? Count(int productId, int countedStock, int userId);

    /// <summary>Xử lý phần thiếu: NormalLoss, Compensation (cần người chịu trách nhiệm và số tiền), Miscount (cần ghi chú).</summary>
    void ResolveShortage(int id, DiscrepancyResolution resolution, string? note, int? responsibleUserId, string? responsibleName, decimal? compensationAmount, int adminId);

    /// <summary>Xử lý phần thừa: UnrecordedPurchase (cần nhà cung cấp, số hóa đơn, giá), OtherIncome, Miscount (cần ghi chú).</summary>
    void ResolveSurplus(int id, DiscrepancyResolution resolution, string? note, int? supplierId, string? invoiceNo, decimal? unitCost, int adminId);

    CompensationPayment CollectCompensation(int id, decimal amount, string? note, int adminId);

    List<StockDiscrepancy> GetDiscrepancies(DateTime from, DateTime to, DiscrepancyStatus? status);

    int CountPending();
}
