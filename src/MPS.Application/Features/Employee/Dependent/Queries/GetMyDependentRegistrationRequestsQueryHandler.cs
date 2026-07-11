using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Employee;
using MPS.Application.Features.Employee.Dependent.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Employee;

namespace MPS.Application.Features.Employee.Dependent.Queries;

public class GetMyDependentRegistrationRequestsQueryHandler : IRequestHandler<GetMyDependentRegistrationRequestsQuery, PagedResult<DependentRegistrationRequestDto>>
{
    private readonly IDependentRegistrationRequestRepository _repository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;

    public GetMyDependentRegistrationRequestsQueryHandler(
        IDependentRegistrationRequestRepository repository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _lookup = lookup;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<DependentRegistrationRequestDto>> Handle(GetMyDependentRegistrationRequestsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var page = request.Page <= 0 ? 1 : request.Page;
        var size = request.Size <= 0 ? 20 : Math.Min(request.Size, 100);
        var (items, total) = await _repository.GetPagedAsync(employeeId, request.Status, page, size, cancellationToken);
        var dtos = items.Select(DependentMapper.ToDto).ToList();
        return new PagedResult<DependentRegistrationRequestDto>(dtos, total, page, size);
    }
}
