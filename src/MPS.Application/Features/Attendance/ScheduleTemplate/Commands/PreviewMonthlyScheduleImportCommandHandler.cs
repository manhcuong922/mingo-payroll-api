using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public sealed class PreviewMonthlyScheduleImportCommandHandler
    : IRequestHandler<PreviewMonthlyScheduleImportCommand, MonthlyScheduleImportPreviewDto>
{
    internal static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly IMonthlyScheduleExcelParser _parser;
    private readonly IUserRepository _userRepository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IMonthlyScheduleImportCache _cache;
    private readonly ILogger<PreviewMonthlyScheduleImportCommandHandler> _logger;

    public PreviewMonthlyScheduleImportCommandHandler(
        IMonthlyScheduleExcelParser parser,
        IUserRepository userRepository,
        IShiftTemplateRepository shiftRepository,
        ICurrentUserService currentUser,
        IMonthlyScheduleImportCache cache,
        ILogger<PreviewMonthlyScheduleImportCommandHandler> logger)
    {
        _parser = parser;
        _userRepository = userRepository;
        _shiftRepository = shiftRepository;
        _currentUser = currentUser;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MonthlyScheduleImportPreviewDto> Handle(
        PreviewMonthlyScheduleImportCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        _logger.LogInformation("[MonthlyImport] step=parse-begin userId={UserId}", userId);
        var parsed = _parser.Parse(request.File);
        _logger.LogInformation(
            "[MonthlyImport] step=parse-done schedule={Schedule} month={Month}/{Year} assignments={Count} parseErrors={ErrCount}",
            parsed.ScheduleName, parsed.Month, parsed.Year, parsed.Assignments.Count, parsed.Errors.Count);

        var errors = parsed.Errors
            .Select(e => new MonthlyScheduleImportErrorDto(
                e.RowIndex, e.ColumnIndex, EmployeeCode: null, WorkDate: null, ShiftCode: null,
                e.Code, e.Message))
            .ToList();

        _logger.LogInformation("[MonthlyImport] step=lookup-employees-begin");
        var employeeIdByCode = await _userRepository.GetEmployeeInformationIdsByCodesAsync(
            parsed.Assignments.Select(a => a.EmployeeCode), cancellationToken);
        _logger.LogInformation("[MonthlyImport] step=lookup-employees-done resolved={Count}", employeeIdByCode.Count);

        _logger.LogInformation("[MonthlyImport] step=lookup-shifts-begin");
        var shiftIdByCode = await _shiftRepository.GetActiveIdsByCodesAsync(
            parsed.Assignments.Select(a => a.ShiftCode), cancellationToken);
        _logger.LogInformation("[MonthlyImport] step=lookup-shifts-done resolved={Count}", shiftIdByCode.Count);

        var operations = new List<BatchShiftOperation>(parsed.Assignments.Count);
        var resolvedItems = new List<MonthlyScheduleImportResolvedItemDto>(parsed.Assignments.Count);
        var skipped = 0;

        foreach (var assignment in parsed.Assignments)
        {
            if (!employeeIdByCode.TryGetValue(assignment.EmployeeCode, out var employeeInfoId))
            {
                errors.Add(new MonthlyScheduleImportErrorDto(
                    assignment.RowIndex, assignment.ColumnIndex,
                    assignment.EmployeeCode, assignment.WorkDate, assignment.ShiftCode,
                    "EMPLOYEE_NOT_FOUND",
                    $"Không tìm thấy nhân viên có EmployeeCode '{assignment.EmployeeCode}' hoặc nhân viên chưa link EmployeeInformation."));
                skipped++;
                continue;
            }

            if (!shiftIdByCode.TryGetValue(assignment.ShiftCode, out var shiftTemplateId))
            {
                errors.Add(new MonthlyScheduleImportErrorDto(
                    assignment.RowIndex, assignment.ColumnIndex,
                    assignment.EmployeeCode, assignment.WorkDate, assignment.ShiftCode,
                    "SHIFT_NOT_FOUND",
                    $"Không tìm thấy ShiftTemplate active có Code '{assignment.ShiftCode}'."));
                skipped++;
                continue;
            }

            operations.Add(new BatchShiftOperation(
                BatchShiftOperationKind.Add, employeeInfoId, assignment.WorkDate, shiftTemplateId));
            resolvedItems.Add(new MonthlyScheduleImportResolvedItemDto(
                assignment.EmployeeCode, assignment.EmployeeName, assignment.WorkDate, assignment.ShiftCode));
        }

        _logger.LogInformation("[MonthlyImport] step=cache-begin operations={OpCount} skipped={Skipped}",
            operations.Count, skipped);
        var token = Guid.NewGuid().ToString("N");
        _cache.Set(token, new MonthlyScheduleImportCacheEntry(
            userId, parsed.ScheduleName, parsed.Year, parsed.Month, operations),
            CacheLifetime);
        _logger.LogInformation("[MonthlyImport] step=done token={Token}", token);

        return new MonthlyScheduleImportPreviewDto(
            PreviewToken: token,
            ScheduleName: parsed.ScheduleName,
            Year: parsed.Year,
            Month: parsed.Month,
            TotalCells: parsed.Assignments.Count,
            ResolvedOperations: operations.Count,
            SkippedCells: skipped,
            Errors: errors,
            ResolvedItems: resolvedItems);
    }
}
