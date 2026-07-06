using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

/// <summary>
/// Bước 1/2 import bảng phân ca tháng:
///   - Parse file Excel, resolve EmployeeCode → EmployeeInformationId và ShiftCode → ShiftTemplateId.
///   - Cache list BatchShiftOperation đã resolve dưới một PreviewToken (TTL ngắn).
///   - Trả tóm tắt + danh sách lỗi parse/lookup để user xem trước khi confirm.
/// KHÔNG ghi DB ở bước này.
/// </summary>
public sealed record PreviewMonthlyScheduleImportCommand(Stream File)
    : IRequest<MonthlyScheduleImportPreviewDto>;
