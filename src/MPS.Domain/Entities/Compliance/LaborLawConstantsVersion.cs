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
    public decimal WorkRateNormalFloor { get; set; } = 1.00m;
    public decimal WorkRateWeeklyRestFloor { get; set; } = 2.00m;
    public decimal WorkRateHolidayFloor { get; set; } = 3.00m;
    public decimal OvertimeMaxHoursPerDay { get; set; } = 4.00m;
    public decimal OvertimeMaxHoursPerMonth { get; set; } = 40.00m;
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
