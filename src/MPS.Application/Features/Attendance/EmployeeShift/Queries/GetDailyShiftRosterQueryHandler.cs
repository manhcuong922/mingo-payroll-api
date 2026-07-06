using MediatR;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.EmployeeShift.Queries;

public class GetDailyShiftRosterQueryHandler : IRequestHandler<GetDailyShiftRosterQuery, IReadOnlyList<EmployeeShiftAssignmentDto>>
{
    private readonly IEmployeeShiftAssignmentRepository _repository;
    private readonly IContractRepository _contractRepository;

    public GetDailyShiftRosterQueryHandler(
        IEmployeeShiftAssignmentRepository repository,
        IContractRepository contractRepository)
    {
        _repository = repository;
        _contractRepository = contractRepository;
    }

    public async Task<IReadOnlyList<EmployeeShiftAssignmentDto>> Handle(GetDailyShiftRosterQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetByDateAsync(request.WorkDate, cancellationToken);
        var employeeIds = items.Select(a => a.EmployeeInformationId).Distinct().ToArray();
        var activeIds = await _contractRepository.GetEmployeeInformationIdsWithActiveContractsAsync(employeeIds, cancellationToken);
        var activeSet = activeIds.ToHashSet();

        return items.Where(a => activeSet.Contains(a.EmployeeInformationId)).Select(a => new EmployeeShiftAssignmentDto(
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
