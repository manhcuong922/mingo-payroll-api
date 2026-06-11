// DESIGN NOTE — tại sao KHÔNG hardcode 4 cột OT rate:
// BLLĐ 2019 Điều 98 hiện định nghĩa: ngày thường (150%), cuối tuần (200%),
// ngày lễ (300%), ban đêm (130%). Nếu luật bổ sung loại OT mới (vd: ca 3 ca,
// OT nước ngoài, OT trong vùng đặc biệt...) thì mỗi loại mới buộc thêm cột DB
// + migration + redeploy → vi phạm NFR-M05.
// [CHANGED] Tách thành bảng con OtRateEntry — key-value per loại OT.
// Engine tra cứu bằng OtRateTypes.* constants; HR nhập label + rate qua UI.

namespace MPS.Domain.Entities.Compliance;

public class OtRateVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsArchived { get; set; } = false;

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }

    /// <summary>
    /// Danh sách các loại OT và hệ số tương ứng.
    /// Engine tra cứu qua OtRateTypes.* constants.
    /// </summary>
    public ICollection<OtRateEntry> Entries { get; set; } = new List<OtRateEntry>();
}
