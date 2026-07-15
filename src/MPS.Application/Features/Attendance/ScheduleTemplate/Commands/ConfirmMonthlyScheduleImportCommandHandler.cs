using System.Globalization;
using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using ScheduleTemplateEntity = MPS.Domain.Entities.Attendance.ScheduleTemplate;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public sealed class ConfirmMonthlyScheduleImportCommandHandler
    : IRequestHandler<ConfirmMonthlyScheduleImportCommand, MonthlyScheduleImportConfirmResult>
{
    private readonly IMonthlyScheduleImportCache _cache;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IScheduleTemplateRepository _scheduleRepository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IRegularHoursCapChecker _capChecker;

    public ConfirmMonthlyScheduleImportCommandHandler(
        IMonthlyScheduleImportCache cache,
        IMediator mediator,
        ICurrentUserService currentUser,
        IClock clock,
        IScheduleTemplateRepository scheduleRepository,
        IShiftTemplateRepository shiftRepository,
        IEmployeeInformationRepository employeeRepository,
        IRegularHoursCapChecker capChecker)
    {
        _cache = cache;
        _mediator = mediator;
        _currentUser = currentUser;
        _clock = clock;
        _scheduleRepository = scheduleRepository;
        _shiftRepository = shiftRepository;
        _employeeRepository = employeeRepository;
        _capChecker = capChecker;
    }

    public async Task<MonthlyScheduleImportConfirmResult> Handle(
        ConfirmMonthlyScheduleImportCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        if (!_cache.TryGet(request.PreviewToken, out var entry) || entry is null)
        {
            throw new NotFoundException(
                "Preview token không tồn tại hoặc đã hết hạn. Hãy gọi lại endpoint preview để lấy token mới.");
        }

        if (entry.CreatedByUserId != userId)
        {
            throw new ForbiddenAccessException("Preview token không thuộc về người dùng hiện tại.");
        }

        if (entry.Operations.Count == 0)
        {
            _cache.Remove(request.PreviewToken);
            throw new DomainValidationException(
                "Preview không có operation nào để import. Kiểm tra lại file excel hoặc danh sách Errors.");
        }

        var now = _clock.UtcNow;
        var batchLabel = BuildBatchLabel(entry, now);
        var fromDate = entry.Operations.Min(o => o.WorkDate);
        var toDate = entry.Operations.Max(o => o.WorkDate);

        await EnsureEmployeesHaveActiveUsersAsync(
            entry.Operations.Select(o => o.EmployeeInformationId).Distinct().ToArray(),
            cancellationToken);

        // Group ops by shift để tạo 1 ScheduleTemplate per shift code.
        var groups = entry.Operations
            .GroupBy(o => o.ShiftTemplateId!.Value)
            .OrderBy(g => g.Key)
            .ToList();

        // Preload tên/code + StandardHours shift để đặt tên ScheduleTemplate và pre-check cap.
        var shiftCodeById = new Dictionary<int, string>();
        var shiftHoursById = new Dictionary<int, decimal>();
        foreach (var shiftId in groups.Select(g => g.Key))
        {
            var shift = await _shiftRepository.GetByIdAsync(shiftId, cancellationToken);
            shiftCodeById[shiftId] = shift?.Code ?? $"SHIFT-{shiftId}";
            shiftHoursById[shiftId] = shift?.StandardHours ?? 0m;
        }

        // Pre-check cap before creating any ScheduleTemplate.
        // If this fails in the loop below, earlier groups may already be committed.
        var deltas = entry.Operations
            .Where(o => o.ShiftTemplateId.HasValue)
            .Select(o => new WeeklyCapDelta(
                o.EmployeeInformationId,
                o.WorkDate,
                shiftHoursById.TryGetValue(o.ShiftTemplateId!.Value, out var h) ? h : 0m))
            .ToList();
        if (deltas.Count > 0)
        {
            var dailyWarnings = await _capChecker.CheckDailyAssignmentsAsync(deltas, cancellationToken);
            var weeklyWarnings = await _capChecker.CheckAssignmentsAsync(deltas, cancellationToken);
            if (!request.AcknowledgeWeeklyOvertimeWarnings
                && (dailyWarnings.Count > 0 || weeklyWarnings.Count > 0))
            {
                throw new RegularHoursCapExceededException(dailyWarnings, weeklyWarnings);
            }
        }

        var groupResults = new List<MonthlyScheduleImportGroupResultDto>(groups.Count);
        var totalApplied = 0;
        var totalSkipped = 0;
        var totalConflicts = 0;

        try
        {
            foreach (var group in groups)
            {
                var shiftId = group.Key;
                var shiftCode = shiftCodeById[shiftId];
                var ops = group.ToList();

                var dates = ops.Select(o => o.WorkDate).Distinct().OrderBy(d => d).ToList();
                var empIds = ops.Select(o => o.EmployeeInformationId).Distinct().OrderBy(i => i).ToList();
                var daysOfMonth = dates.Select(d => d.Day).Distinct().OrderBy(d => d).ToList();

                var st = new ScheduleTemplateEntity
                {
                    Name = TruncateName($"{batchLabel} — {shiftCode}"),
                    ShiftTemplateId = shiftId,
                    EffectiveFrom = dates.First(),
                    EffectiveTo = dates.Last(),
                    RepeatUnit = ScheduleRepeatUnit.Month,
                    RepeatEvery = 1,
                    DaysOfWeekMask = null,
                    DaysOfMonthMask = string.Join(",", daysOfMonth),
                    IsActive = true,
                    Notes = $"Auto-created from monthly import — {entry.ScheduleName ?? "(không tên)"}.",
                    CreatedAt = now,
                    CreatedByUserId = userId,
                };

                var newTemplateId = await _scheduleRepository.AddAsync(st, empIds, new[] { shiftId }, cancellationToken);

                // Re-emit operations với SourceScheduleTemplateId trỏ về template vừa tạo.
                var opsForBatch = ops
                    .Select(o => new BatchShiftOperation(
                        o.Kind, o.EmployeeInformationId, o.WorkDate, o.ShiftTemplateId))
                    .ToList();

                var batchResult = await _mediator.Send(
                    new BatchUpdateShiftAssignmentsCommand(
                        opsForBatch,
                        BatchShiftConflictPolicy.Fail,
                        newTemplateId,
                        AcknowledgeWeeklyOvertimeWarnings: true),
                    cancellationToken);

                totalApplied += batchResult.Applied;
                totalSkipped += batchResult.Skipped;
                totalConflicts += batchResult.Conflicts;

                groupResults.Add(new MonthlyScheduleImportGroupResultDto(
                    ScheduleTemplateId: newTemplateId,
                    ScheduleTemplateName: st.Name,
                    ShiftTemplateId: shiftId,
                    ShiftTemplateCode: shiftCode,
                    DaysOfMonth: daysOfMonth,
                    EmployeeCount: empIds.Count,
                    Applied: batchResult.Applied,
                    Skipped: batchResult.Skipped,
                    Conflicts: batchResult.Conflicts));
            }

            return new MonthlyScheduleImportConfirmResult(
                BatchName: batchLabel,
                FromDate: fromDate,
                ToDate: toDate,
                TemplateCount: groupResults.Count,
                Applied: totalApplied,
                Skipped: totalSkipped,
                Conflicts: totalConflicts,
                Groups: groupResults);
        }
        finally
        {
            // Token chỉ dùng 1 lần — luôn xoá kể cả khi throw.
            _cache.Remove(request.PreviewToken);
        }
    }

    private async Task EnsureEmployeesHaveActiveUsersAsync(
        IReadOnlyCollection<int> employeeIds,
        CancellationToken cancellationToken)
    {
        var ids = employeeIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0) return;

        var activeIds = await _employeeRepository.GetEmployeeInformationIdsWithActiveUserAsync(ids, cancellationToken);
        var activeSet = activeIds.ToHashSet();
        var inactiveIds = ids.Where(id => !activeSet.Contains(id)).ToArray();
        if (inactiveIds.Length == 0) return;

        var preview = string.Join(", ", inactiveIds.Take(10));
        var suffix = inactiveIds.Length > 10 ? $" và {inactiveIds.Length - 10} nhân viên khác" : string.Empty;
        throw new DomainValidationException(
            $"Không thể import phân ca: nhân viên {preview}{suffix} không có tài khoản active.");
    }

    private static string BuildBatchLabel(MonthlyScheduleImportCacheEntry entry, DateTime now)
    {
        var monthLabel = entry.Year.HasValue && entry.Month.HasValue
            ? $"{entry.Month:D2}/{entry.Year}"
            : now.ToString("MM/yyyy", CultureInfo.InvariantCulture);
        var prefix = string.IsNullOrWhiteSpace(entry.ScheduleName) ? "Import" : entry.ScheduleName!.Trim();
        // Timestamp để name unique khi import lại cùng 1 file/tháng.
        var ts = now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        return $"{prefix} ({monthLabel}) #{ts}";
    }

    // Schedule Template Name max 150 chars (xem ScheduleTemplateEntityConfiguration).
    private static string TruncateName(string name) =>
        name.Length <= 150 ? name : name[..150];
}
