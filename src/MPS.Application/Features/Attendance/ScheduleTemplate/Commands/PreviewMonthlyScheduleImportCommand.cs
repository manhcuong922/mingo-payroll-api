using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

/// <summary>
/// Bước 1/2 import bảng phân ca tháng:
///   - Employees mode: parse file, resolve EmployeeCode → EmployeeInformationId và
///     ShiftCode → ShiftTemplateId, cache ops (mỗi row = 1 NV).
///   - Departments mode: parse file, resolve DepartmentCode → DepartmentId, expand
///     mỗi row thành N ops (foreach NV active trong dept), cache thêm map DeptId
///     per op để Confirm biết dept nguồn.
/// KHÔNG ghi DB ở bước này.
/// </summary>
public sealed record PreviewMonthlyScheduleImportCommand(
    Stream File,
    ScheduleApplyTarget ApplyTarget = ScheduleApplyTarget.Employees)
    : IRequest<MonthlyScheduleImportPreviewDto>;
