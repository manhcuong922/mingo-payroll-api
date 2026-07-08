using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public class GetEligibleEmployeesForOvertimeQueryHandler
    : IRequestHandler<GetEligibleEmployeesForOvertimeQuery, IReadOnlyList<EligibleEmployeeForOvertimeDto>>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetEligibleEmployeesForOvertimeQueryHandler(
        IProjectRepository projectRepository,
        IEmployeeInformationRepository employeeRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EligibleEmployeeForOvertimeDto>> Handle(
        GetEligibleEmployeesForOvertimeQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var pmEmployeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");
        var pm = await _employeeRepository.GetByIdAsync(pmEmployeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee {pmEmployeeId} not found.");

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException($"Project {request.ProjectId} not found.");
        if (project.DepartmentId != pm.DepartmentId)
            throw new ForbiddenAccessException("PM không thuộc phòng ban của project này.");

        if (project.DepartmentId is not int deptId)
            return Array.Empty<EligibleEmployeeForOvertimeDto>();

        var employees = await _employeeRepository.GetByDepartmentIdAsync(deptId, cancellationToken);
        // Loại bỏ chính PM khỏi picker — PM không tự giao OT cho mình
        return employees
            .Where(e => e.Id != pmEmployeeId)
            .Select(e => new EligibleEmployeeForOvertimeDto(
                e.Id,
                e.FullName,
                e.User?.EmployeeCode,
                e.Position?.Name))
            .ToList();
    }
}
