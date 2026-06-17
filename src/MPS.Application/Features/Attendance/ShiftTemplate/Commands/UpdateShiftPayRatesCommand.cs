using MediatR;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public sealed record UpdateShiftPayRatesCommand(
    int ShiftTemplateId,
    decimal WorkRateNormal,
    decimal WorkRateWeeklyRest,
    decimal WorkRateHoliday) : IRequest<bool>;
