using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using OvertimeRequestEntity = MPS.Domain.Entities.Attendance.OvertimeRequest;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// FT-12 (Dieu 107 BLLD 2019).
/// Employee tu chon ngay + gio OT. BR-5 khong overlap ca, BR-6 cap ngay,
/// BR-7 cap thang, BR-8 cap nam.
/// </summary>
public class CreateOvertimeRequestCommandHandler : IRequestHandler<CreateOvertimeRequestCommand, int>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly OvertimeCapChecker _capChecker;
    private readonly IContractLookupReader _lookup;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        OvertimeCapChecker capChecker,
        IContractLookupReader lookup,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _capChecker = capChecker;
        _lookup = lookup;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var otHours = CalculateHours(request.OtStartTime, request.OtEndTime);

        await _capChecker.CheckAsync(
            employeeId,
            request.WorkDate,
            request.OtStartTime,
            request.OtEndTime,
            otHours,
            cancellationToken);

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);
        var breakdown = _splitter.Split(new WorkHourSplitInput(
            CheckIn: request.OtStartTime,
            CheckOut: request.OtEndTime,
            BreakStart: null,
            BreakEnd: null,
            NightStart: laborLaw.NightStartTime,
            NightEnd: laborLaw.NightEndTime));

        var entity = new OvertimeRequestEntity
        {
            EmployeeInformationId = employeeId,
            WorkDate = request.WorkDate,
            OvertimeSlotTemplateId = null,
            OtStartTime = request.OtStartTime,
            OtEndTime = request.OtEndTime,
            OtHours = otHours,
            OtDayHours = breakdown.DayHours,
            OtNightHours = breakdown.NightHours,
            Status = OtRequestStatus.Pending,
            Reason = request.Reason,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }

    private static decimal CalculateHours(TimeOnly start, TimeOnly end)
    {
        var spanMinutes = end > start
            ? (end - start).TotalMinutes
            : (24 * 60 - (start - TimeOnly.MinValue).TotalMinutes) + (end - TimeOnly.MinValue).TotalMinutes;

        return Math.Round((decimal)spanMinutes / 60m, 2);
    }
}
