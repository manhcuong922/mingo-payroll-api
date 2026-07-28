using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public class GetMyDayOffStatusQueryHandler
    : IRequestHandler<GetMyDayOffStatusQuery, DayOffStatusDto>
{
    private readonly IEmployeeDayOffChecker _dayOffChecker;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public GetMyDayOffStatusQueryHandler(
        IEmployeeDayOffChecker dayOffChecker,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _dayOffChecker = dayOffChecker;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DayOffStatusDto> Handle(
        GetMyDayOffStatusQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var date = request.Date ?? TodayInVietnam();

        return await _dayOffChecker.CheckAsync(employeeId, date, cancellationToken);
    }

    /// <summary>Ngày hiện tại theo giờ VN — cùng quy ước với FaceCheckinCommandHandler.</summary>
    private DateOnly TodayInVietnam()
    {
        var vnZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc), vnZone);
        return DateOnly.FromDateTime(localNow);
    }
}
