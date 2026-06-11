namespace MPS.Domain.Entities.Compliance;

/// <summary>
/// Versioned container cho tất cả hằng số luật lao động có thể thay đổi theo thời gian.
/// Không hardcode trong engine — đọc qua IComplianceLookupService.GetLaborLawConstantsAt().
/// </summary>
public class LaborLawConstantsVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsArchived { get; set; } = false;

    // BLLĐ Điều 26 — lương thử việc tối thiểu = X% lương hợp đồng
    public decimal ProbationSalaryMinPercent { get; set; } = 85.00m;

    // BLLĐ Điều 98 khoản 3 — phụ cấp làm đêm tối thiểu = X% lương giờ ban ngày
    public decimal NightShiftSurchargePercent { get; set; } = 20.00m;

    // NĐ 145/2020 — số giờ làm việc tiêu chuẩn trong 1 ngày
    public decimal StandardWorkingHoursPerDay { get; set; } = 8.00m;

    // Số ngày làm việc tiêu chuẩn trong 1 tháng (quy đổi tháng → ngày → giờ)
    public decimal StandardWorkingDaysPerMonth { get; set; } = 26.00m;

    // BLLĐ Điều 113 — số ngày nghỉ phép tối thiểu hàng năm
    public int AnnualLeaveMinDays { get; set; } = 12;

    // BLLĐ Điều 107 — giờ OT tối đa trong 1 ngày
    public decimal OvertimeMaxHoursPerDay { get; set; } = 4.00m;

    // BLLĐ Điều 107 — giờ OT tối đa trong 1 năm
    public int OvertimeMaxHoursPerYear { get; set; } = 300;

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
