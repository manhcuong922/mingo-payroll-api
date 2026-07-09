using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeSlotTemplate.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using OvertimeSlotTemplateEntity = MPS.Domain.Entities.Attendance.OvertimeSlotTemplate;

namespace MPS.Application.Features.Attendance.OvertimeSlotTemplate.Commands;

public sealed class CreateOvertimeSlotTemplateCommandHandler : IRequestHandler<CreateOvertimeSlotTemplateCommand, int>
{
    private readonly IOvertimeSlotTemplateRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateOvertimeSlotTemplateCommandHandler(
        IOvertimeSlotTemplateRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateOvertimeSlotTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var code = await OvertimeSlotCodeGenerator.GenerateUniqueAsync(
            request.StartTime, request.EndTime, _repository, excludeId: null, cancellationToken);

        var now = _clock.UtcNow;

        // Auto-inactive các slot active khác có khung giờ giao thoa — tránh 2 slot cùng cover
        // 1 khoảng thời gian gây ambiguity cho payroll composer.
        await _repository.DeactivateOverlappingActiveAsync(
            request.StartTime, request.EndTime, excludeId: null, userId, now, cancellationToken);

        var entity = new OvertimeSlotTemplateEntity
        {
            Code = code,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsNightSlot = request.IsNightSlot,
            // Null-out inactive side theo IsNightSlot để composer không đọc nhầm.
            MultiplierNormalDay              = request.IsNightSlot ? null : request.MultiplierNormalDay,
            MultiplierWeeklyRestDay          = request.IsNightSlot ? null : request.MultiplierWeeklyRestDay,
            MultiplierHolidayDay             = request.IsNightSlot ? null : request.MultiplierHolidayDay,
            MultiplierNormalNightNoPrevOt    = request.IsNightSlot ? request.MultiplierNormalNightNoPrevOt    : null,
            MultiplierNormalNightWithPrevOt  = request.IsNightSlot ? request.MultiplierNormalNightWithPrevOt  : null,
            MultiplierWeeklyRestNight        = request.IsNightSlot ? request.MultiplierWeeklyRestNight        : null,
            MultiplierHolidayNight           = request.IsNightSlot ? request.MultiplierHolidayNight           : null,
            Description = request.Description,
            IsActive = true,
            CreatedAt = now,
            CreatedByUserId = userId,
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}

public sealed class UpdateOvertimeSlotTemplateCommandHandler : IRequestHandler<UpdateOvertimeSlotTemplateCommand, bool>
{
    private readonly IOvertimeSlotTemplateRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdateOvertimeSlotTemplateCommandHandler(
        IOvertimeSlotTemplateRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(UpdateOvertimeSlotTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        var code = await OvertimeSlotCodeGenerator.GenerateUniqueAsync(
            request.StartTime, request.EndTime, _repository, excludeId: request.Id, cancellationToken);

        var now = _clock.UtcNow;

        // Auto-inactive slot khác giao thoa — chỉ khi entity đang được set active. Nếu user
        // update sang inactive thì không cần đụng slot khác.
        if (request.IsActive)
        {
            await _repository.DeactivateOverlappingActiveAsync(
                request.StartTime, request.EndTime, excludeId: request.Id, userId, now, cancellationToken);
        }

        var entity = new OvertimeSlotTemplateEntity
        {
            Id = request.Id,
            Code = code,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsNightSlot = request.IsNightSlot,
            MultiplierNormalDay              = request.IsNightSlot ? null : request.MultiplierNormalDay,
            MultiplierWeeklyRestDay          = request.IsNightSlot ? null : request.MultiplierWeeklyRestDay,
            MultiplierHolidayDay             = request.IsNightSlot ? null : request.MultiplierHolidayDay,
            MultiplierNormalNightNoPrevOt    = request.IsNightSlot ? request.MultiplierNormalNightNoPrevOt    : null,
            MultiplierNormalNightWithPrevOt  = request.IsNightSlot ? request.MultiplierNormalNightWithPrevOt  : null,
            MultiplierWeeklyRestNight        = request.IsNightSlot ? request.MultiplierWeeklyRestNight        : null,
            MultiplierHolidayNight           = request.IsNightSlot ? request.MultiplierHolidayNight           : null,
            Description = request.Description,
            IsActive = request.IsActive,
            UpdatedAt = now,
            UpdatedByUserId = userId,
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}

public sealed class DeleteOvertimeSlotTemplateCommandHandler : IRequestHandler<DeleteOvertimeSlotTemplateCommand, bool>
{
    private readonly IOvertimeSlotTemplateRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public DeleteOvertimeSlotTemplateCommandHandler(
        IOvertimeSlotTemplateRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(DeleteOvertimeSlotTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        // Nếu đã có OvertimeRequest reference → chỉ deactivate (giữ audit). Ngược lại
        // có thể hard-delete; ở đây luôn deactivate cho đơn giản.
        var referenced = await _repository.IsReferencedAsync(request.Id, cancellationToken);
        if (referenced)
        {
            return await _repository.DeactivateAsync(request.Id, userId, _clock.UtcNow, cancellationToken);
        }

        return await _repository.DeactivateAsync(request.Id, userId, _clock.UtcNow, cancellationToken);
    }
}
