using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.PublicHoliday.Commands;

/// <summary>
/// Bước 1/2 import ngày lễ: parse file, kiểm tra shape + trùng lịch (in-file duplicates,
/// overlap với DB), trả preview cho FE. Không ghi DB.
/// </summary>
public sealed record PreviewPublicHolidayImportCommand(Stream File)
    : IRequest<PublicHolidayImportPreviewDto>;
