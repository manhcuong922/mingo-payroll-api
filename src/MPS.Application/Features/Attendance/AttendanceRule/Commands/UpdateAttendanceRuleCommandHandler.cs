using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.AttendanceRule.Common;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRuleEntity = MPS.Domain.Entities.Attendance.AttendanceRule;
using AttendanceRuleVersionEntity = MPS.Domain.Entities.Attendance.AttendanceRuleVersion;

namespace MPS.Application.Features.Attendance.AttendanceRule.Commands;

public class UpdateAttendanceRuleCommandHandler : IRequestHandler<UpdateAttendanceRuleCommand, bool>
{
    private readonly IAttendanceRuleRepository _repository;
    private readonly IAttendanceRuleVersionRepository _versionRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IComplianceLookupService _complianceLookup;

    public UpdateAttendanceRuleCommandHandler(
        IAttendanceRuleRepository repository,
        IAttendanceRuleVersionRepository versionRepository,
        ICurrentUserService currentUser,
        IClock clock,
        IComplianceLookupService complianceLookup)
    {
        _repository = repository;
        _versionRepository = versionRepository;
        _currentUser = currentUser;
        _clock = clock;
        _complianceLookup = complianceLookup;
    }

    public async Task<bool> Handle(UpdateAttendanceRuleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.IsActive && !request.IsActive)
        {
            var activeShiftNames = await _repository.GetActiveShiftTemplateNamesUsingRuleAsync(request.Id, cancellationToken);
            if (activeShiftNames.Count > 0)
                throw new ConflictException(AttendanceRuleUsageMessage.Build(existing.Name, activeShiftNames));
        }

        var name = request.Name.Trim();
        if (await _repository.ExistsByNameAsync(name, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"Attendance rule with name '{name}' already exists.");

        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // So sánh rate với open version; nếu đổi → close cũ + tạo mới. Nếu chưa có version nào (edge case) → tạo mới.
        var openVersion = await _versionRepository.GetOpenAsync(request.Id, cancellationToken);
        var rateChanged = openVersion is null
            || openVersion.WorkRateNormal != request.WorkRateNormal
            || openVersion.WorkRateWeeklyRest != request.WorkRateWeeklyRest
            || openVersion.WorkRateHoliday != request.WorkRateHoliday;

        if (rateChanged)
        {
            var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);
            WorkRateFloorValidator.EnsureValid(
                request.WorkRateNormal,
                request.WorkRateWeeklyRest,
                request.WorkRateHoliday,
                laborLaw.WorkRateNormalFloor,
                laborLaw.WorkRateWeeklyRestFloor,
                laborLaw.WorkRateHolidayFloor);

            if (openVersion is not null)
            {
                // Nếu open version bắt đầu hôm nay → không thể close trước ngày mở (would set EffectiveTo < EffectiveFrom).
                // Trong case đó, chỉ update rate trực tiếp version đang mở (chưa từng có ngày làm việc nào áp dụng).
                if (openVersion.EffectiveFrom >= today)
                {
                    throw new DomainValidationException(
                        $"Version của rule '{existing.Name}' vừa được tạo hôm nay ({today:yyyy-MM-dd}) — không thể close+create version mới trong cùng ngày. Vui lòng đợi sang ngày kế tiếp hoặc chỉnh Name/Description trước.");
                }
                await _versionRepository.CloseOpenVersionAsync(request.Id, today.AddDays(-1), userId, now, cancellationToken);
            }

            var newVersion = new AttendanceRuleVersionEntity
            {
                AttendanceRuleId = request.Id,
                EffectiveFrom = today,
                EffectiveTo = null,
                WorkRateNormal = request.WorkRateNormal,
                WorkRateWeeklyRest = request.WorkRateWeeklyRest,
                WorkRateHoliday = request.WorkRateHoliday,
                CreatedAt = now,
                CreatedByUserId = userId,
            };
            await _versionRepository.AddAsync(newVersion, cancellationToken);
        }

        var entity = new AttendanceRuleEntity
        {
            Id = request.Id,
            Name = name,
            Description = request.Description,
            IsActive = request.IsActive,
            UpdatedAt = now,
            UpdatedByUserId = userId
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
