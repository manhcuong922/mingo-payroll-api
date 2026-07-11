using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Employee;
using MPS.Application.Features.Employee.Dependent.Common;
using MPS.Domain.Interfaces.Employee;

namespace MPS.Application.Features.Employee.Dependent.Queries;

public class GetDependentRegistrationRequestsQueryHandler : IRequestHandler<GetDependentRegistrationRequestsQuery, PagedResult<DependentRegistrationRequestDto>>
{
    private readonly IDependentRegistrationRequestRepository _repository;

    public GetDependentRegistrationRequestsQueryHandler(IDependentRegistrationRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<DependentRegistrationRequestDto>> Handle(GetDependentRegistrationRequestsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);
        var (items, total) = await _repository.GetPagedAsync(
            request.EmployeeInformationId, request.Status, page, size, cancellationToken);
        var dtos = items.Select(DependentMapper.ToDto).ToList();
        return new PagedResult<DependentRegistrationRequestDto>(dtos, total, page, size);
    }
}
