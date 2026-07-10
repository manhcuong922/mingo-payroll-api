using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public class GetPmProjectsQueryHandler : IRequestHandler<GetPmProjectsQuery, IReadOnlyList<PmProjectDto>>
{
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetPmProjectsQueryHandler(
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

    public async Task<IReadOnlyList<PmProjectDto>> Handle(GetPmProjectsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var pmEmployeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");
        var pm = await _employeeRepository.GetByIdAsync(pmEmployeeId, cancellationToken)
            ?? throw new NotFoundException($"Employee {pmEmployeeId} not found.");

        if (pm.DepartmentId is not int deptId)
            return Array.Empty<PmProjectDto>();

        // Chỉ trả project mà user hiện tại là tổ trưởng phụ trách (ResponsibleUserId).
        // PM đơn khác trong cùng xưởng KHÔNG được thấy — tránh giao OT chéo dự án.
        var projects = await _projectRepository.GetByDepartmentAsync(deptId, cancellationToken);
        return projects
            .Where(p => p.ResponsibleUserId == userId)
            .Select(p => new PmProjectDto(
                p.Id,
                p.Title,
                p.DepartmentId,
                p.Department?.Name,
                p.Status.ToString()))
            .ToList();
    }
}
