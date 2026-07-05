using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Queries;

public class DepartmentEligibleManagersQueryHandler
    : IRequestHandler<DepartmentEligibleManagersQuery, IReadOnlyList<EligibleManagerDto>>
{
    private readonly IDepartmentRepository _repository;

    public DepartmentEligibleManagersQueryHandler(IDepartmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<EligibleManagerDto>> Handle(
        DepartmentEligibleManagersQuery request, CancellationToken cancellationToken)
    {
        var candidates = await _repository.GetEligibleManagersAsync(
            request.Keyword, SystemRoles.EligibleManagerRoleIds, cancellationToken);

        return candidates
            .Select(c => new EligibleManagerDto(c.Id, c.FullName, c.PositionName, c.RoleId, c.RoleName))
            .ToList();
    }
}
