using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CHTLManager.DAL.Entities;

public enum DiscrepancyStatus
{
    Pending = 1,
    Resolved = 2
}

public enum DiscrepancyResolution
{
    /// <summary>Thiếu: hao hụt, hỏng, hết hạn, mất không rõ người. Tính vào chi phí.</summary>
    NormalLoss = 1,
    /// <summary>Thiếu: có người chịu trách nhiệm bồi thường. Vẫn là chi phí, tiền thu được ghi riêng.</summary>
    Compensation = 2,
    /// <summary>Đếm sai trước đó: không phải chi phí nhưng vẫn hiện riêng trong báo cáo để không che được thất thoát.</summary>
    Miscount = 3,
    /// <summary>Thừa: lô hàng chưa ghi. Sinh phiếu nhập và công nợ, không cộng kho lần nữa.</summary>
    UnrecordedPurchase = 4,
    /// <summary>Thừa: không rõ nguồn. Ghi nhận thu nhập khác theo giá vốn.</summary>
    OtherIncome = 5
}

/// <summary>Chênh lệch giữa sổ và số đếm thực tế. Tồn kho đã đổi ngay khi đếm; bước xử lý quyết định con số đi về đâu.</summary>
public class StockDiscrepancy
{
    public int Id { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int CountedByUserId { get; set; }
    public User? CountedBy { get; set; }
    public DateTime CountedAt { get; set; }

    public int BookStock { get; set; }
    public int CountedStock { get; set; }

    /// <summary>Số đếm trừ số trên sổ: âm là thiếu, dương là thừa.</summary>
    public int Delta { get; set; }

    /// <summary>Giá vốn một đơn vị lúc đếm, dùng để tính giá trị chênh lệch.</summary>
    public decimal UnitCost { get; set; }

    public DiscrepancyStatus Status { get; set; }
    public DiscrepancyResolution? Resolution { get; set; }

    public int? ResolvedByUserId { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public int? ResponsibleUserId { get; set; }
    public User? ResponsibleUser { get; set; }

    /// <summary>Người chịu trách nhiệm không có tài khoản trong hệ thống.</summary>
    [MaxLength(100)]
    public string? ResponsibleName { get; set; }

    /// <summary>Số tiền phải bồi thường. App chỉ theo dõi, không tự trừ lương.</summary>
    public decimal? CompensationAmount { get; set; }

    public List<CompensationPayment> Payments { get; set; } = new();

    [NotMapped]
    public decimal Value => Math.Abs(Delta) * UnitCost;

    [NotMapped]
    public bool IsShortage => Delta < 0;

    /// <summary>Người đếm cũng là người xử lý: vẫn được phép ở cửa hàng ít người, nhưng hiện rõ để chủ cửa hàng xem.</summary>
    [NotMapped]
    public bool IsSelfResolved => ResolvedByUserId == CountedByUserId;

    [NotMapped]
    public decimal CompensationCollected => Payments.Sum(p => p.Amount);

    [NotMapped]
    public decimal CompensationRemaining => (CompensationAmount ?? 0) - CompensationCollected;

    [NotMapped]
    public string ResponsibleDisplay => ResponsibleUser?.FullName ?? ResponsibleName ?? string.Empty;

    [NotMapped]
    public string StockChange => $"{Product.FormatStock(BookStock)} → {CountedStock}";

    [NotMapped]
    public string DeltaText => Delta > 0 ? $"+{Delta}" : Delta.ToString();

    [NotMapped]
    public string StatusName => Status == DiscrepancyStatus.Pending ? "Chờ xử lý" : "Đã xử lý";

    [NotMapped]
    public string ResolutionName => Resolution switch
    {
        DiscrepancyResolution.NormalLoss => "Thất thoát",
        DiscrepancyResolution.Compensation => "Bồi thường",
        DiscrepancyResolution.Miscount => "Đếm sai",
        DiscrepancyResolution.UnrecordedPurchase => "Lô chưa ghi",
        DiscrepancyResolution.OtherIncome => "Thu nhập khác",
        _ => string.Empty
    };

    [NotMapped]
    public string SelfResolvedText => Status == DiscrepancyStatus.Resolved && IsSelfResolved ? "Tự xử lý" : string.Empty;
}

/// <summary>Một lần thu tiền bồi thường.</summary>
public class CompensationPayment
{
    public int Id { get; set; }

    public int StockDiscrepancyId { get; set; }
    public StockDiscrepancy? StockDiscrepancy { get; set; }

    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}
