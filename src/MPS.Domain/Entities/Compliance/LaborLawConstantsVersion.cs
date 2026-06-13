using MPS.Domain.Interfaces;

namespace MPS.Domain.Entities.Compliance;

public sealed class LaborLawConstantsVersion : IComplianceVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }

    public decimal ProbationSalaryMinPercent { get; set; } = 85.00m;
    public decimal NightShiftSurchargePercent { get; set; } = 20.00m;
    public decimal StandardWorkingHoursPerDay { get; set; } = 8.00m;
    public decimal StandardWorkingDaysPerMonth { get; set; } = 26.00m;
    public int AnnualLeaveMinDays { get; set; } = 12;
    public decimal OvertimeMaxHoursPerDay { get; set; } = 4.00m;
    public int OvertimeMaxHoursPerYear { get; set; } = 300;

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}
