using MediatR;

namespace MPS.Application.Features.Compliance.LaborLawConstants.Commands;

public sealed record CreateLaborLawConstantsCommand(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    decimal ProbationSalaryMinPercent,
    decimal OvertimeNightExtraPercent,
    decimal NightWorkSurchargePercent,
    decimal OvertimeNormalUpliftPercent,
    TimeOnly NightStartTime,
    TimeOnly NightEndTime,
    decimal StandardWorkingHoursPerDay,
    decimal MaxRegularHoursPerWeek,
    decimal StandardWorkingDaysPerMonth,
    int AnnualLeaveMinDays,
    decimal WorkRateNormalFloor,
    decimal WorkRateWeeklyRestFloor,
    decimal WorkRateHolidayFloor,
    decimal OvertimeMaxHoursPerDay,
    decimal OvertimeMaxHoursPerMonth,
    int OvertimeMaxHoursPerYear,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds) : IRequest<int>;

public sealed record UpdateLaborLawConstantsCommand(
    int Id,
    DateOnly ValidFrom,
    decimal ProbationSalaryMinPercent,
    decimal OvertimeNightExtraPercent,
    decimal NightWorkSurchargePercent,
    decimal OvertimeNormalUpliftPercent,
    TimeOnly NightStartTime,
    TimeOnly NightEndTime,
    decimal StandardWorkingHoursPerDay,
    decimal MaxRegularHoursPerWeek,
    decimal StandardWorkingDaysPerMonth,
    int AnnualLeaveMinDays,
    decimal WorkRateNormalFloor,
    decimal WorkRateWeeklyRestFloor,
    decimal WorkRateHolidayFloor,
    decimal OvertimeMaxHoursPerDay,
    decimal OvertimeMaxHoursPerMonth,
    int OvertimeMaxHoursPerYear,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds) : IRequest<bool>;

public sealed record DeleteLaborLawConstantsPendingCommand(int Id) : IRequest<bool>;
