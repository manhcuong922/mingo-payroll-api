using MediatR;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Commands;

public sealed record ApproveLateEarlyRequestCommand(
    int Id,
    string? ReviewNote,
    string? OverrideReason) : IRequest<ApproveLateEarlyRequestResult>;

/// <summary>
/// ⭐ LATE-EARLY-2026-08 — chỉ còn cờ <paramref name="Approved"/>. Hai trường
/// <c>MustDeductBecauseQuotaExceeded</c> / <c>Warning</c> đã bỏ cùng với quota nội quy: không còn
/// đường nào để duyệt-nhưng-không-thành-công, nên duyệt hỏng nay là exception chứ không phải 422
/// mang payload.
/// </summary>
public sealed record ApproveLateEarlyRequestResult(bool Approved);
