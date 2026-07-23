using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

public sealed record CreateLeaveRequestCommand(
    int LeaveTypeId,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Reason,
    string? SupportingDocumentUrl,
    string? MedicalCertificateUrl,
    string? InsuranceCertNumber,
    OvertimeApproverRoute? ApproverRoute,
    string? OverrideReason) : IRequest<CreateLeaveRequestResult>;

/// <summary>
/// Kết quả tạo đơn kèm cờ báo hệ thống đã tự chuyển sang loại UNPAID (khi hết quỹ phép + LeaveType.AutoFallbackToUnpaid=true).
///
/// <para><b>`[D2]` — đơn bị TÁCH khi xin vượt quỹ.</b> Nếu người lao động còn quỹ phép nhưng xin
/// nhiều hơn số còn lại, hệ thống tạo <b>hai</b> đơn: phần đầu dùng nốt quỹ (có lương) và phần
/// vượt là nghỉ không lương. Khi đó <see cref="Id"/> là đơn CÓ LƯƠNG, còn
/// <see cref="UnpaidId"/> là đơn không lương; cả hai đều ở trạng thái chờ duyệt.
/// Khi không tách (còn đủ quỹ, hoặc hết sạch quỹ) thì <see cref="UnpaidId"/> = <c>null</c> và
/// <see cref="Id"/> là đơn duy nhất — giữ nguyên hợp đồng cũ của API.</para>
/// </summary>
public sealed record CreateLeaveRequestResult(
    int Id,
    int FinalLeaveTypeId,
    string FinalLeaveTypeCode,
    bool SwitchedToUnpaid,
    string? Message,
    int? UnpaidId = null,
    decimal? PaidDays = null,
    decimal? UnpaidDays = null);
