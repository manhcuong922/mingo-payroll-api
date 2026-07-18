using MediatR;

namespace MPS.Application.Common.Models.Attendance;

/// <summary>
/// Publish khi <see cref="Domain.Entities.EmployeeInformation.DepartmentId"/> đổi
/// (Fresh assign, Change A→B, Leave X→null, Delete NV). Handler auto-apply lịch phân ca
/// sẽ gỡ ca dept cũ và apply ca dept mới nếu có template match.
///
/// KHÔNG publish khi <c>OldDepartmentId == NewDepartmentId</c> (idempotent).
///
/// <see cref="ChangedByUserId"/> = user thực hiện create/update/delete NV; assignment và warning
/// row dùng userId này để audit. <see cref="ChangedAtUtc"/> = thời điểm change (UTC).
/// </summary>
public sealed record EmployeeDepartmentChangedNotification(
    int EmployeeInformationId,
    int? OldDepartmentId,
    int? NewDepartmentId,
    int ChangedByUserId,
    DateTime ChangedAtUtc) : INotification;
