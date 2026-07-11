using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Common;

/// <summary>
/// Enforce quota nội quy công ty cho đơn Late/Early:
///  - Số lần Approved trong tháng cùng Kind ≤ AttendanceSetting.LateEarlyMaxOccurrencesPerMonth (mặc định 2).
///  - Minutes ≤ AttendanceSetting.LateEarlyMaxMinutesPerOccurrence (mặc định 30).
/// Nếu vượt bất kỳ ngưỡng nào → yêu cầu approver set DeductFromLeaveBalance = true.
/// </summary>
public sealed class LateEarlyLimitChecker
{
    public const int DefaultMaxOccurrencesPerMonth = 2;
    public const int DefaultMaxMinutesPerOccurrence = 30;

    private readonly ILateEarlyRequestRepository _repository;
    private readonly IAttendanceSettingRepository _settingRepository;

    public LateEarlyLimitChecker(
        ILateEarlyRequestRepository repository,
        IAttendanceSettingRepository settingRepository)
    {
        _repository = repository;
        _settingRepository = settingRepository;
    }

    public async Task<LateEarlyLimitCheckResult> CheckAsync(
        int employeeInformationId,
        DateOnly workDate,
        LateEarlyKind kind,
        short minutes,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var setting = await _settingRepository.GetOrCreateDefaultAsync(nowUtc, cancellationToken);
        var maxOccurrences = setting.LateEarlyMaxOccurrencesPerMonth ?? DefaultMaxOccurrencesPerMonth;
        var maxMinutes = setting.LateEarlyMaxMinutesPerOccurrence ?? DefaultMaxMinutesPerOccurrence;

        var approvedCount = await _repository.CountApprovedInMonthAsync(
            employeeInformationId, workDate.Year, workDate.Month, kind, cancellationToken);

        var exceedsOccurrences = approvedCount >= maxOccurrences;
        var exceedsMinutes = minutes > maxMinutes;
        var mustDeduct = exceedsOccurrences || exceedsMinutes;

        return new LateEarlyLimitCheckResult(
            approvedCount,
            maxOccurrences,
            maxMinutes,
            exceedsOccurrences,
            exceedsMinutes,
            mustDeduct);
    }
}

public sealed record LateEarlyLimitCheckResult(
    int ApprovedInMonth,
    int MaxOccurrencesPerMonth,
    int MaxMinutesPerOccurrence,
    bool ExceedsOccurrences,
    bool ExceedsMinutes,
    bool MustDeductFromLeaveBalance);
