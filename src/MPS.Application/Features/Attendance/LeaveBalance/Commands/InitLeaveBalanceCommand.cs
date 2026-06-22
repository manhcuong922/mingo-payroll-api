using MediatR;

namespace MPS.Application.Features.Attendance.LeaveBalance.Commands;

/// <summary>
/// HR khởi tạo balance cho 1 NV trong 1 năm với 1 loại nghỉ phép cụ thể.
/// LeaveType.DeductsBalance phải = true. BaseDays + SeniorityBonusDays cho mã ANNUAL
/// do handler tự tính từ join date (BR-15); các mã khác BaseDays = 0 và HR có thể adjust riêng.
/// </summary>
public sealed record InitLeaveBalanceCommand(
    int EmployeeInformationId,
    int Year,
    int LeaveTypeId) : IRequest<int>;
