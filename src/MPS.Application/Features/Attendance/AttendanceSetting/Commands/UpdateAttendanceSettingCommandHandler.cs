using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.AttendanceSetting.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using AttendanceSettingEntity = MPS.Domain.Entities.Attendance.AttendanceSetting;

namespace MPS.Application.Features.Attendance.AttendanceSetting.Commands;

public sealed class UpdateAttendanceSettingCommandHandler
    : IRequestHandler<UpdateAttendanceSettingCommand, AttendanceSettingDto>
{
    private readonly IAttendanceSettingRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdateAttendanceSettingCommandHandler(
        IAttendanceSettingRepository repository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AttendanceSettingDto> Handle(
        UpdateAttendanceSettingCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var current = await _repository.GetOrCreateDefaultAsync(_clock.UtcNow, cancellationToken);
        var entity = new AttendanceSettingEntity
        {
            Id = current.Id,
            IsEditLockEnabled = request.IsEditLockEnabled,
            DailyEditCutoffTime = request.DailyEditCutoffTime,
            LockAfterDays = request.LockAfterDays,
            AllowAdminOverride = request.AllowAdminOverride,
            RequireOverrideReason = request.RequireOverrideReason,
            TimezoneId = string.IsNullOrWhiteSpace(request.TimezoneId)
                ? "Asia/Ho_Chi_Minh"
                : request.TimezoneId.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = current.CreatedAt,
            CreatedByUserId = current.CreatedByUserId,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId,
        };

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return AttendanceSettingMapper.ToDto(updated);
    }
}
