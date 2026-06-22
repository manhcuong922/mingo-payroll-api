using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveRequestEntity = MPS.Domain.Entities.Attendance.LeaveRequest;

namespace MPS.Application.Features.Attendance.LeaveRequest.Commands;

/// <summary>
/// FT-13 — tạo yêu cầu nghỉ phép theo cấu hình LeaveType động.
/// Rule (MaxDaysPerRequest, RequireDocument, IsPaid, DeductsBalance) đọc từ bảng LeaveTypes.
/// </summary>
public class CreateLeaveRequestCommandHandler : IRequestHandler<CreateLeaveRequestCommand, int>
{
    private readonly ILeaveRequestRepository _repository;
    private readonly ILeaveTypeRepository _leaveTypeRepository;
    private readonly ILeaveBalanceRepository _balanceRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateLeaveRequestCommandHandler(
        ILeaveRequestRepository repository,
        ILeaveTypeRepository leaveTypeRepository,
        ILeaveBalanceRepository balanceRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _leaveTypeRepository = leaveTypeRepository;
        _balanceRepository = balanceRepository;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        if (request.EndDate < request.StartDate)
            throw new DomainValidationException("EndDate must be on/after StartDate.");

        var leaveType = await _leaveTypeRepository.GetByIdAsync(request.LeaveTypeId, cancellationToken)
            ?? throw new NotFoundException($"Leave type {request.LeaveTypeId} not found.");
        if (!leaveType.IsActive)
            throw new DomainValidationException($"Leave type '{leaveType.Code}' is inactive.");

        // TotalDays = số ngày trong khoảng trừ Chủ nhật
        var totalDays = CountWorkingDays(request.StartDate, request.EndDate);
        if (totalDays <= 0)
            throw new DomainValidationException("Leave period must contain at least one working day (non-Sunday).");

        if (leaveType.MaxDaysPerRequest is decimal max && totalDays > max)
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' allows at most {max} day(s) per request; requested {totalDays}.");

        if (leaveType.RequireDocument && string.IsNullOrWhiteSpace(request.SupportingDocumentUrl))
            throw new DomainValidationException(
                $"Leave type '{leaveType.Code}' requires a SupportingDocumentUrl.");

        if (leaveType.DeductsBalance)
        {
            var balance = await _balanceRepository.GetByKeyAsync(
                employeeId, request.StartDate.Year, leaveType.Id, cancellationToken);
            var remaining = (balance?.TotalDays ?? 0m) - (balance?.UsedDays ?? 0m);
            if (remaining < totalDays)
                throw new DomainValidationException(
                    $"Insufficient '{leaveType.Code}' balance. Remaining={remaining}, requested={totalDays}.");
        }

        var entity = new LeaveRequestEntity
        {
            EmployeeInformationId = employeeId,
            LeaveTypeId = leaveType.Id,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalDays = totalDays,
            IsPaid = leaveType.IsPaid,  // snapshot tại thời điểm tạo
            Status = LeaveRequestStatus.Pending,
            Reason = request.Reason,
            SupportingDocumentUrl = request.SupportingDocumentUrl,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }

    private static decimal CountWorkingDays(DateOnly start, DateOnly end)
    {
        var count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Sunday) count++;
        }
        return count;
    }
}
