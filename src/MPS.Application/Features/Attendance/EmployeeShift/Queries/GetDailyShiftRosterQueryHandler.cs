using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.EmployeeShift.Queries;

public class GetDailyShiftRosterQueryHandler : IRequestHandler<GetDailyShiftRosterQuery, IReadOnlyList<EmployeeShiftAssignmentDto>>
{
    private readonly IEmployeeShiftAssignmentRepository _repository;

    public GetDailyShiftRosterQueryHandler(IEmployeeShiftAssignmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<EmployeeShiftAssignmentDto>> Handle(GetDailyShiftRosterQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByDateAsync(request.WorkDate, cancellationToken);

        return items.Select(a => new EmployeeShiftAssignmentDto(
            a.Id,
            a.EmployeeInformationId,
            a.EmployeeInformation.FullName,
            a.ShiftTemplateId,
            a.ShiftTemplate.Name,
            a.ShiftTemplate.StartTime,
            a.ShiftTemplate.EndTime,
            a.WorkDate,
            a.CreatedAt)).ToList();
    }
}
