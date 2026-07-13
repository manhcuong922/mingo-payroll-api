using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public sealed class GetMyOvertimeYearlySummaryQueryHandler
    : IRequestHandler<GetMyOvertimeYearlySummaryQuery, MyOvertimeYearlySummaryDto>
{
    private static readonly IReadOnlyCollection<OtRequestStatus> ApprovedOnly =
        new[] { OtRequestStatus.Approved };

    private static readonly IReadOnlyCollection<OtRequestStatus> PendingBoth =
        new[] { OtRequestStatus.Pending, OtRequestStatus.PendingApprover };

    private readonly IOvertimeRequestRepository _otRepository;
    private readonly IContractLookupReader _lookup;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public GetMyOvertimeYearlySummaryQueryHandler(
        IOvertimeRequestRepository otRepository,
        IContractLookupReader lookup,
        IComplianceLookupService complianceLookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _otRepository = otRepository;
        _lookup = lookup;
        _complianceLookup = complianceLookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<MyOvertimeYearlySummaryDto> Handle(
        GetMyOvertimeYearlySummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var today = DateOnly.FromDateTime(_clock.UtcNow);

        var todayApproved = await _otRepository.GetTotalOtHoursOnDateAsync(
            employeeId, today, ApprovedOnly, excludeId: null, cancellationToken);
        var todayPending = await _otRepository.GetTotalOtHoursOnDateAsync(
            employeeId, today, PendingBoth, excludeId: null, cancellationToken);

        var monthApproved = await _otRepository.GetTotalOtHoursInMonthAsync(
            employeeId, today.Year, today.Month, ApprovedOnly, cancellationToken);
        var monthPending = await _otRepository.GetTotalOtHoursInMonthAsync(
            employeeId, today.Year, today.Month, PendingBoth, cancellationToken);

        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);

        return new MyOvertimeYearlySummaryDto(
            Today:               today,
            TodayApprovedHours:  todayApproved,
            TodayPendingHours:   todayPending,
            DailyCap:            laborLaw.OvertimeMaxHoursPerDay,
            MonthApprovedHours:  monthApproved,
            MonthPendingHours:   monthPending,
            MonthlyCap:          laborLaw.OvertimeMaxHoursPerMonth);
    }
}
