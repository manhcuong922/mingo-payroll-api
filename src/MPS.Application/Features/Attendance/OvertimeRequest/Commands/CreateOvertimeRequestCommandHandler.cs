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
/// FT-12 (Điều 107 BLLĐ 2019).
/// BR-5 không overlap ca, BR-6 cap ngày, BR-7 cap tháng, BR-8 cap năm.
/// BR-21: Employee tự tạo ⇒ ngầm đồng ý (Điều 107.2.a).
/// </summary>
public class CreateOvertimeRequestCommandHandler : IRequestHandler<CreateOvertimeRequestCommand, int>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly OvertimeCapChecker _capChecker;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        OvertimeCapChecker capChecker,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _capChecker = capChecker;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        if (request.OtEndTime <= request.OtStartTime)
            throw new DomainValidationException("OtEndTime must be greater than OtStartTime.");

        var otHours = Math.Round((decimal)(request.OtEndTime - request.OtStartTime).TotalHours, 2);

        // BR-5/6/7/8
        await _capChecker.CheckAsync(employeeId, request.WorkDate,
            request.OtStartTime, request.OtEndTime, otHours, cancellationToken);

        var entity = new OvertimeRequestEntity
        {
            EmployeeInformationId = employeeId,
            WorkDate = request.WorkDate,
            OtStartTime = request.OtStartTime,
            OtEndTime = request.OtEndTime,
            OtHours = otHours,
            Status = OtRequestStatus.Pending,
            Reason = request.Reason,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
