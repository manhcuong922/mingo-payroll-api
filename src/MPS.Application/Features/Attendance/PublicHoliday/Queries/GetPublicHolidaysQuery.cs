using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.PublicHoliday.Queries;

public sealed record GetPublicHolidaysQuery(int Year, bool IncludeInactive = false) : IRequest<IReadOnlyList<PublicHolidayDto>>;
