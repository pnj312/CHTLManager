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

    /// <summary>Hủy lần thu ghi nhầm, bắt buộc có lý do: số còn phải thu tăng lại, dòng vẫn được giữ để đối chiếu.</summary>
    void CancelCompensationPayment(int paymentId, string reason, int adminId);

    /// <summary>
    /// Sửa người chịu trách nhiệm hoặc số tiền của khoản bồi thường, bắt buộc có lý do và ghi nhật ký.
    /// Không giảm dưới số đã thu; không đổi người khi còn lần thu có hiệu lực, vì tiền đó do người cũ trả.
    /// </summary>
    void CorrectCompensation(int id, int? responsibleUserId, string? responsibleName, decimal amount, string reason, int adminId);

    /// <summary>Mọi lần thu của một phiếu chênh lệch, kể cả lần đã hủy, mới nhất trước.</summary>
    List<CompensationPayment> GetCompensationPayments(int discrepancyId);

    List<StockDiscrepancy> GetDiscrepancies(DateTime from, DateTime to, DiscrepancyStatus? status);

    int CountPending();
}
