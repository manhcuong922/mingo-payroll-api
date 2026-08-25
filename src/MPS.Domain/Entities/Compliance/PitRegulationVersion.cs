using MPS.Domain.Attributes;
using MPS.Domain.Interfaces;

namespace MPS.Domain.Entities.Compliance;

[Auditable]
public sealed class PitRegulationVersion : IComplianceVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }

    public decimal PersonalDeductionAmount { get; set; }
    public decimal DependentDeductionAmount { get; set; }

    public int ShortTermContractMaxMonths { get; set; } = 3;
    public decimal ShortTermFlatRatePercent { get; set; } = 10.00m;
    public decimal ShortTermTaxExemptMonthlyIncome { get; set; } = 2_000_000m;

    /// <summary>
    /// Trần phần tiền thuê nhà do DN trả thay được tính vào thu nhập chịu thuế — theo % của TỔNG
    /// thu nhập chịu thuế CHƯA GỒM tiền nhà (TT 111/2013 Đ.11.1). Phần vượt trần được MIỄN.
    ///
    /// <para>0 = KHÔNG áp trần, cùng quy ước với trần bảo hiểm: kỳ lương / phiên bản luật tạo trước
    /// bản sửa này không có giá trị nên phải giữ nguyên hành vi cũ thay vì miễn sạch tiền nhà.</para>
    /// </summary>
    public decimal HousingExemptRatePercent { get; set; } = 15.00m;

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<PitBracket> Brackets { get; set; } = [];
}
