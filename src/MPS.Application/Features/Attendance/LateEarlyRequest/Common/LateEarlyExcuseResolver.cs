using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Common;

/// <summary>
/// Gạch tổng số phút được MIỄN cho (nhân viên, ngày) từ toàn bộ đơn Late/Early đã
/// Approved. Kết quả truyền cho <c>AttendanceHourCalculator.Compute</c> để dời cửa
/// sổ hiệu dụng ⇒ NV không mất công của phần đã được duyệt trễ/sớm.
///
/// Tách khỏi calculator để bản thân calculator vẫn là pure function (không I/O).
/// </summary>
public sealed class LateEarlyExcuseResolver
{
    private readonly ILateEarlyRequestRepository _repository;

    public LateEarlyExcuseResolver(ILateEarlyRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<LateEarlyExcuseMinutes> GetExcusedMinutesAsync(
        int employeeInformationId,
        DateOnly workDate,
        CancellationToken cancellationToken = default)
    {
        var approved = await _repository.GetApprovedByEmployeeAndDateAsync(
            employeeInformationId, workDate, cancellationToken);

        var lateMinutes = 0;
        var earlyMinutes = 0;
        foreach (var request in approved)
        {
            switch (request.Kind)
            {
                case LateEarlyKind.LateArrival:
                    lateMinutes += request.Minutes;
                    break;
                case LateEarlyKind.EarlyLeave:
                    earlyMinutes += request.Minutes;
                    break;
            }
        }

        return new LateEarlyExcuseMinutes(lateMinutes, earlyMinutes);
    }
}

public readonly record struct LateEarlyExcuseMinutes(int LateMinutes, int EarlyMinutes)
{
    public static LateEarlyExcuseMinutes Zero => new(0, 0);
}
