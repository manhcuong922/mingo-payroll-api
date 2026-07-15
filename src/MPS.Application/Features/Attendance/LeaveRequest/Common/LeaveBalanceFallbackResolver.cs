using MPS.Application.Features.Attendance.LeaveType.Common;
using MPS.Domain.Interfaces.Attendance;
using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;

namespace MPS.Application.Features.Attendance.LeaveRequest.Common;

/// <summary>
/// Khi employee nộp đơn Leave mà hết quỹ phép, resolver này quyết định
/// chuyển sang loại UNPAID (nếu LeaveType.AutoFallbackToUnpaid=true và tồn tại LeaveType UNPAID).
/// Trả về null nếu không cần / không thể fallback (handler sẽ throw balance-insufficient như cũ).
/// </summary>
public sealed class LeaveBalanceFallbackResolver
{
    public const string UnpaidLeaveTypeCode = "UNPAID";

    private readonly ILeaveTypeRepository _leaveTypeRepository;
    private readonly ILeaveBalanceRepository _balanceRepository;

    public LeaveBalanceFallbackResolver(
        ILeaveTypeRepository leaveTypeRepository,
        ILeaveBalanceRepository balanceRepository)
    {
        _leaveTypeRepository = leaveTypeRepository;
        _balanceRepository = balanceRepository;
    }

    public async Task<LeaveTypeEntity?> ResolveFallbackAsync(
        LeaveTypeEntity requestedType,
        int employeeInformationId,
        int year,
        decimal requestedDays,
        CancellationToken cancellationToken)
    {
        if (!requestedType.AutoFallbackToUnpaid) return null;
        if (!LeaveTypeAssignmentPolicy.TracksYearlyBalance(requestedType)) return null;

        var balance = await _balanceRepository.GetByKeyAsync(
            employeeInformationId, year, requestedType.Id, cancellationToken);
        var remaining = (balance?.TotalDays ?? 0m) - (balance?.UsedDays ?? 0m);
        if (remaining >= requestedDays) return null;

        var unpaid = await _leaveTypeRepository.GetByCodeAsync(UnpaidLeaveTypeCode, cancellationToken);
        if (unpaid is null || !unpaid.IsActive) return null;

        return unpaid;
    }
}
