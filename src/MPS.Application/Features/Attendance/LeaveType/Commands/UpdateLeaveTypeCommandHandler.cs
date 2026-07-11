using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.LeaveType.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using LeaveTypeEntity = MPS.Domain.Entities.Attendance.LeaveType;

namespace MPS.Application.Features.Attendance.LeaveType.Commands;

public class UpdateLeaveTypeCommandHandler : IRequestHandler<UpdateLeaveTypeCommand, bool>
{
    private readonly ILeaveTypeRepository _repository;
    private readonly ILeaveBalanceRepository _balanceRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdateLeaveTypeCommandHandler(
        ILeaveTypeRepository repository,
        ILeaveBalanceRepository balanceRepository,
        IEmployeeInformationRepository employeeRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _balanceRepository = balanceRepository;
        _employeeRepository = employeeRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(UpdateLeaveTypeCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        if (await _repository.ExistsByCodeAsync(code, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"Leave type with code '{code}' already exists.");
        if (await _repository.ExistsByNameAsync(name, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"Leave type with name '{name}' already exists.");

        var entity = new LeaveTypeEntity
        {
            Id = request.Id,
            Code = code,
            Name = name,
            Description = request.Description,
            IsPaid = request.IsPaid,
            PaidRate = request.IsPaid ? request.PaidRate : null,
            MaxDaysPerRequest = request.MaxDaysPerRequest,
            AllowEmployeeSelection = request.AllowEmployeeSelection,
            MaxDaysPerYear = request.MaxDaysPerYear,
            MaxDaysPerMonth = request.MaxDaysPerMonth,
            MaxDaysPerWeek = request.MaxDaysPerWeek,
            ApplyScope = request.ApplyScope,
            TargetDepartmentIds = request.TargetDepartmentIds,
            TargetEmployeeIds = request.TargetEmployeeIds,
            RequireDocument = request.RequireDocument,
            DeductsBalance = request.DeductsBalance || request.MaxDaysPerYear.HasValue,
            MinNoticeDaysForShort = request.MinNoticeDaysForShort,
            MinNoticeDaysForMedium = request.MinNoticeDaysForMedium,
            MinNoticeDaysForLong = request.MinNoticeDaysForLong,
            AutoFallbackToUnpaid = request.AutoFallbackToUnpaid,
            AllowRetroactive = request.AllowRetroactive,
            IsActive = request.IsActive,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId
        };

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        if (updated)
            await SyncCurrentYearBalancesAsync(entity, userId, cancellationToken);

        return updated;
    }

    private async Task SyncCurrentYearBalancesAsync(
        LeaveTypeEntity entity,
        int userId,
        CancellationToken cancellationToken)
    {
        if (!entity.IsActive || !LeaveTypeAssignmentPolicy.TracksYearlyBalance(entity))
            return;

        var totalDays = LeaveTypeAssignmentPolicy.ResolveYearlyQuota(entity);
        if (totalDays <= 0m) return;

        var employees = await _employeeRepository.GetAllAsync(cancellationToken: cancellationToken);
        var targetIds = LeaveTypeAssignmentPolicy.ResolveTargetEmployeeIds(
            entity,
            employees);

        await _balanceRepository.UpsertForLeaveTypeAsync(
            entity.Id,
            _clock.UtcNow.Year,
            targetIds,
            totalDays,
            userId,
            _clock.UtcNow,
            cancellationToken);
    }
}
