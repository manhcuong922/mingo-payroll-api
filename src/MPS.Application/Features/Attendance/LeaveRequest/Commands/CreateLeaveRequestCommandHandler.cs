using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveRequest.Common;
using MPS.Application.Features.Attendance.LeaveType.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveRequestEntity = MPS.Domain.Entities.Attendance.LeaveRequest;
using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

/// <summary>
/// FT-13 — tạo yêu cầu nghỉ phép theo cấu hình LeaveType động.
/// Bổ sung: notice-period (SLA nội quy), retroactive gate (Sick/Maternity), auto-fallback UNPAID,
/// và cutoff enforcement khi StartDate không thuộc retroactive.
/// </summary>
public class CreateLeaveRequestCommandHandler : IRequestHandler<CreateLeaveRequestCommand, CreateLeaveRequestResult>
{
    private readonly ILeaveRequestRepository _repository;
    private readonly ILeaveTypeRepository _leaveTypeRepository;
    private readonly ILeaveBalanceRepository _balanceRepository;
    private readonly IContractLookupReader _lookup;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IDayTypeResolver _dayTypeResolver;
    private readonly IAttendanceEditLockChecker _editLockChecker;
    private readonly LeaveBalanceFallbackResolver _fallbackResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateLeaveRequestCommandHandler(
        ILeaveRequestRepository repository,
        ILeaveTypeRepository leaveTypeRepository,
        ILeaveBalanceRepository balanceRepository,
        IContractLookupReader lookup,
        IEmployeeInformationRepository employeeRepository,
        IDayTypeResolver dayTypeResolver,
        IAttendanceEditLockChecker editLockChecker,
        LeaveBalanceFallbackResolver fallbackResolver,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _leaveTypeRepository = leaveTypeRepository;
        _balanceRepository = balanceRepository;
        _lookup = lookup;
        _employeeRepository = employeeRepository;
        _dayTypeResolver = dayTypeResolver;
        _editLockChecker = editLockChecker;
        _fallbackResolver = fallbackResolver;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<CreateLeaveRequestResult> Handle(CreateLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");
        var employee = await _employeeRepository.GetByIdAsync(employeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee profile {employeeId} not found.");

        if (request.EndDate < request.StartDate)
            throw new DomainValidationException("EndDate must be on/after StartDate.");

        var leaveType = await _leaveTypeRepository.GetByIdAsync(request.LeaveTypeId, cancellationToken)
            ?? throw new NotFoundException($"Leave type {request.LeaveTypeId} not found.");
        if (!leaveType.IsActive)
            throw new DomainValidationException($"Leave type '{leaveType.Code}' is inactive.");
        if (!leaveType.AllowEmployeeSelection)
            throw new DomainValidationException($"Leave type '{leaveType.Code}' is not available for employee self-service.");
        if (!LeaveTypeAssignmentPolicy.AppliesToEmployee(leaveType, employeeId, employee.DepartmentId))
            throw new DomainValidationException($"Leave type '{leaveType.Code}' is not assigned to this employee.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.AddHours(7));
        var isRetroactive = request.StartDate < today;
        if (isRetroactive && !leaveType.AllowRetroactive)
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' không cho phép nộp ngày đã qua.");

        var totalDays = await CountWorkingDaysAsync(employeeId, request.StartDate, request.EndDate, cancellationToken);
        if (totalDays <= 0)
            throw new DomainValidationException("Leave period must contain at least one working day (non-weekly-rest-day).");

        if (leaveType.MaxDaysPerRequest is decimal max && totalDays > max)
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' allows at most {max} day(s) per request; requested {totalDays}.");

        if (leaveType.RequireDocument
            && string.IsNullOrWhiteSpace(request.SupportingDocumentUrl)
            && string.IsNullOrWhiteSpace(request.MedicalCertificateUrl))
        {
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' requires a SupportingDocumentUrl or MedicalCertificateUrl.");
        }

        if (!isRetroactive)
        {
            LeaveNoticePeriodChecker.EnsureCompliant(leaveType, request.StartDate, today, totalDays);
            await _editLockChecker.EnsureCanModifyAsync(request.StartDate, request.OverrideReason, cancellationToken);
        }

        var finalType = leaveType;
        var switched = false;
        string? switchMessage = null;

        if (LeaveTypeAssignmentPolicy.TracksYearlyBalance(leaveType))
        {
            var balance = await _balanceRepository.GetByKeyAsync(
                employeeId, request.StartDate.Year, leaveType.Id, cancellationToken);
            var yearlyQuota = LeaveTypeAssignmentPolicy.ResolveYearlyQuota(leaveType);
            if ((balance is null || balance.TotalDays <= 0m) && yearlyQuota > 0m)
            {
                await _balanceRepository.UpsertForLeaveTypeAsync(
                    leaveType.Id,
                    request.StartDate.Year,
                    new[] { employeeId },
                    yearlyQuota,
                    userId,
                    _clock.UtcNow,
                    cancellationToken);
                balance = await _balanceRepository.GetByKeyAsync(
                    employeeId, request.StartDate.Year, leaveType.Id, cancellationToken);
            }

            var remaining = (balance?.TotalDays ?? yearlyQuota) - (balance?.UsedDays ?? 0m);
            if (remaining < totalDays)
            {
                var fallback = await _fallbackResolver.ResolveFallbackAsync(
                    leaveType, employeeId, request.StartDate.Year, totalDays, cancellationToken);
                if (fallback is null)
                {
                    throw new DomainValidationException(
                        $"Insufficient '{leaveType.Code}' balance. Remaining={remaining}, requested={totalDays}.");
                }

                finalType = fallback;
                switched = true;
                switchMessage = $"Đã hết quỹ '{leaveType.Code}' (còn {remaining}). "
                    + $"Hệ thống tự chuyển đơn sang loại '{fallback.Code}' (nghỉ không lương).";
            }
        }

        var entity = new LeaveRequestEntity
        {
            EmployeeInformationId = employeeId,
            LeaveTypeId = finalType.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = totalDays,
            IsPaid = finalType.IsPaid,
            Status = LeaveRequestStatus.Pending,
            Reason = request.Reason,
            SupportingDocumentUrl = request.SupportingDocumentUrl,
            MedicalCertificateUrl = request.MedicalCertificateUrl,
            InsuranceCertNumber = request.InsuranceCertNumber?.Trim(),
            IsRetroactive = isRetroactive,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        var id = await _repository.AddAsync(entity, cancellationToken);
        return new CreateLeaveRequestResult(id, finalType.Id, finalType.Code, switched, switchMessage);
    }

    private async Task<decimal> CountWorkingDaysAsync(
        int employeeInformationId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var dayTypes = await _dayTypeResolver.ResolveRangeAsync(employeeInformationId, start, end, cancellationToken);
        return dayTypes.Count(kv => kv.Value != DayType.WeeklyRestDay);
    }
}
