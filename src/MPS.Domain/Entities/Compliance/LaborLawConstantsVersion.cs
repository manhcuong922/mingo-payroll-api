using MPS.Domain.Attributes;
using MPS.Domain.Interfaces;

namespace MPS.Domain.Entities.Compliance;

[Auditable]
public sealed class LaborLawConstantsVersion : IComplianceVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }

    public decimal ProbationSalaryMinPercent { get; set; } = 85.00m;
    public decimal OvertimeNightExtraPercent { get; set; } = 20.00m;
    public decimal NightWorkSurchargePercent { get; set; } = 30.00m;
    public decimal OvertimeNormalUpliftPercent { get; set; } = 50.00m;
    public TimeOnly NightStartTime { get; set; } = new(22, 0);
    public TimeOnly NightEndTime { get; set; } = new(6, 0);
    public decimal StandardWorkingHoursPerDay { get; set; } = 8.00m;
    public decimal MaxRegularHoursPerWeek { get; set; } = 48.00m;
    public decimal StandardWorkingDaysPerMonth { get; set; } = 26.00m;
    public int AnnualLeaveMinDays { get; set; } = 12;
    public decimal WorkRateNormalFloor { get; set; } = 100.00m;
    public decimal WorkRateWeeklyRestFloor { get; set; } = 200.00m;
    public decimal WorkRateHolidayFloor { get; set; } = 300.00m;
    public decimal OvertimeMaxHoursPerDay { get; set; } = 4.00m;
    public decimal OvertimeMaxHoursPerMonth { get; set; } = 40.00m;
    /// <summary>
    /// ⭐ OT-YEAR-CAP-2026-08 — mặc định 200h/năm, KHÔNG phải 300h.
    ///
    /// <para>Đ.107.3 BLLĐ đặt trần chung là 200 giờ/năm; 300 giờ chỉ dành cho danh mục ngành được
    /// liệt kê (dệt may, da giày, điện tử, chế biến nông–lâm–thuỷ sản, điện, viễn thông, lọc dầu,
    /// cấp thoát nước…) và doanh nghiệp còn phải THÔNG BÁO BẰNG VĂN BẢN cho Sở LĐTBXH
    /// (NĐ 145/2020 Đ.62). Để 300 làm mặc định là hệ thống lặng lẽ cho vượt 200h với doanh nghiệp
    /// không thuộc diện — <c>OvertimeCapChecker</c> tin tuyệt đối vào con số này.</para>
    ///
    /// <para>Doanh nghiệp thuộc diện 300h vẫn khai được: sửa hằng số của phiên bản luật đang áp
    /// dụng và ghi căn cứ vào <see cref="LegalReference"/> — một quyết định CÓ HỒ SƠ, đúng như
    /// nghĩa vụ thông báo mà luật đòi.</para>
    /// </summary>
    public int OvertimeMaxHoursPerYear { get; set; } = 200;

    /// <summary>
    /// Phạm vi miễn thuế TNCN cho tiền tăng ca:
    /// <list type="bullet">
    ///   <item><c>false</c> (luật cũ, TT 111/2013 Đ.3) — chỉ miễn PHẦN CHÊNH LỆCH vượt 100%;
    ///     phần lương gốc tương ứng giờ làm bình thường vẫn chịu thuế.</item>
    ///   <item><c>true</c> (Luật TNCN 109/2025/QH15 + NĐ 253/2026/NĐ-CP, từ 01/07/2026) — miễn
    ///     TOÀN BỘ tiền tăng ca.</item>
    /// </list>
    /// Điều kiện "trong hạn mức giờ OT hợp pháp" của luật mới đã được bảo đảm sẵn ở tầng tạo/duyệt đơn
    /// OT (<c>OvertimeCapChecker</c> chặn theo <see cref="OvertimeMaxHoursPerMonth"/>/
    /// <see cref="OvertimeMaxHoursPerYear"/>), nên mọi đơn OT đến được payroll đều đã hợp lệ.
    /// </summary>
    public bool OvertimeIncomeFullyTaxExempt { get; set; }

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
