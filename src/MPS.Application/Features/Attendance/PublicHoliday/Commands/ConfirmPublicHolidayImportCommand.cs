using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

/// <summary>
/// Bước 2/2 import: nhận list rows đã được FE lọc valid từ preview, re-validate + bulk insert.
/// </summary>
public sealed record ConfirmPublicHolidayImportCommand(IReadOnlyList<PublicHolidayImportConfirmRow> Rows)
    : IRequest<PublicHolidayImportConfirmResultDto>;
