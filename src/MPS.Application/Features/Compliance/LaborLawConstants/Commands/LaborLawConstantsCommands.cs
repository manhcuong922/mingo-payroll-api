using MediatR;

namespace MPS.Application.Features.Compliance.LaborLawConstants.Commands;

public sealed record CreateLaborLawConstantsCommand(
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
    IReadOnlyList<int> LegalDocumentIds) : IRequest<int>;

public sealed record UpdateLaborLawConstantsCommand(
    int Id,
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
    IReadOnlyList<int> LegalDocumentIds) : IRequest<bool>;

public sealed record DeleteLaborLawConstantsPendingCommand(int Id) : IRequest<bool>;
