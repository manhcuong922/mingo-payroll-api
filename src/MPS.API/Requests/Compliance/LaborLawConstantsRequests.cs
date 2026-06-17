namespace MPS.API.Requests.Compliance;

public sealed record CreateLaborLawConstantsRequest(
    DateOnly ValidFrom,
    bool ApplyImmediately,
    decimal ProbationSalaryMinPercent,
    decimal NightShiftSurchargePercent,
    decimal StandardWorkingHoursPerDay,
    decimal StandardWorkingDaysPerMonth,
    int AnnualLeaveMinDays,
    decimal OvertimeMaxHoursPerDay,
    int OvertimeMaxHoursPerYear,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);

public sealed record UpdateLaborLawConstantsRequest(
    DateOnly ValidFrom,
    decimal ProbationSalaryMinPercent,
    decimal NightShiftSurchargePercent,
    decimal StandardWorkingHoursPerDay,
    decimal StandardWorkingDaysPerMonth,
    int AnnualLeaveMinDays,
    decimal OvertimeMaxHoursPerDay,
    int OvertimeMaxHoursPerYear,
    string? LegalReference,
    string? Note,
    IReadOnlyList<int> LegalDocumentIds);
