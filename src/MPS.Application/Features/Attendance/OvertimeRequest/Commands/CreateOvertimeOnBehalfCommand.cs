using FluentValidation;
using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// Case 6 — Tổ trưởng (Project.ResponsibleUserId) đăng ký OT HỘ cho một NV trong đơn mình phụ trách.
/// Khác luồng NV self-create: đơn VÀO THẲNG <c>PendingApprover</c> (bỏ bước PM-review vì chính tổ
/// trưởng đã đứng ra tạo), ApproverRoute chọn ngay lúc tạo. Giữ BR-10: tổ trưởng là người tạo kiêm
/// PM ⇒ không được là người duyệt cuối.
/// </summary>
public sealed record CreateOvertimeOnBehalfCommand(
    int ProjectId,
    int EmployeeInformationId,
    DateOnly WorkDate,
    TimeOnly OtStartTime,
    TimeOnly OtEndTime,
    OvertimeApproverRoute ApproverRoute,
    string? Reason) : IRequest<int>;

public sealed class CreateOvertimeOnBehalfCommandValidator : AbstractValidator<CreateOvertimeOnBehalfCommand>
{
    public CreateOvertimeOnBehalfCommandValidator()
    {
        RuleFor(x => x.ProjectId).GreaterThan(0).WithMessage("Phải chọn đơn sản xuất.");
        RuleFor(x => x.EmployeeInformationId).GreaterThan(0).WithMessage("Phải chọn nhân viên.");
        RuleFor(x => x.ApproverRoute).IsInEnum().WithMessage("Phải chọn route duyệt cuối (HR hoặc DM).");
        RuleFor(x => x)
            .Must(x => x.OtStartTime != x.OtEndTime)
            .WithMessage("Giờ kết thúc phải khác giờ bắt đầu.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
