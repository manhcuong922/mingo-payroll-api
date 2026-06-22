using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.EmployeeShift.Queries;

public class GetShiftRosterQueryHandler : IRequestHandler<GetShiftRosterQuery, ShiftRosterDto>
{
    private const int MaxRosterDays = 366;

    private readonly IEmployeeShiftAssignmentRepository _repository;

    public GetShiftRosterQueryHandler(IEmployeeShiftAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<ShiftRosterDto> Handle(GetShiftRosterQuery request, CancellationToken cancellationToken)
    {
        if (request.EmployeeInformationIds is null || request.EmployeeInformationIds.Count == 0)
        {
            throw new DomainValidationException("EmployeeInformationIds không được rỗng.");
        }
        if (request.ToDate < request.FromDate)
        {
            throw new DomainValidationException("ToDate phải ≥ FromDate.");
        }
        var span = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;
        if (span > MaxRosterDays)
        {
            throw new DomainValidationException($"Khoảng ngày tối đa {MaxRosterDays} ngày, hiện {span}.");
        }

        var employeeIds = request.EmployeeInformationIds.Distinct().ToList();

        var assignments = await _repository.GetByEmployeesAndDateRangeAsync(
            employeeIds, request.FromDate, request.ToDate, cancellationToken);
        var lockedRows = await _repository.GetLockedShiftsAsync(
            employeeIds, request.FromDate, request.ToDate, cancellationToken);

        var assignmentDtos = assignments.Select(a => new EmployeeShiftAssignmentDto(
            a.Id,
            a.EmployeeInformationId,
            a.EmployeeInformation?.FullName ?? string.Empty,
            a.ShiftTemplateId,
            a.ShiftTemplate.Name,
            a.ShiftTemplate.StartTime,
            a.ShiftTemplate.EndTime,
            a.WorkDate,
            a.CreatedAt)).ToList();

        var lockedDtos = lockedRows
            .Select(r => new ShiftRosterLockedDateDto(r.EmployeeInformationId, r.WorkDate, r.ShiftTemplateId))
            .ToList();

        return new ShiftRosterDto(request.FromDate, request.ToDate, employeeIds, assignmentDtos, lockedDtos);
    }
}
