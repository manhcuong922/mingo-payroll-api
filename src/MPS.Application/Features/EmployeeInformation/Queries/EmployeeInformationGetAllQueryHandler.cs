using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Queries;

public class EmployeeInformationGetAllQueryHandler
    : IRequestHandler<EmployeeInformationGetAllQuery, IReadOnlyList<EmployeeInformationDto>>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public EmployeeInformationGetAllQueryHandler(
        IEmployeeInformationRepository repository,
        IMapper mapper,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _mapper = mapper;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<EmployeeInformationDto>> Handle(
        EmployeeInformationGetAllQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.HasPermission(Permissions.EmployeeInformationReadAll))
        {
            var all = await _repository.GetAllAsync(cancellationToken);
            return _mapper.Map<IReadOnlyList<EmployeeInformationDto>>(all);
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId is null)
            return Array.Empty<EmployeeInformationDto>();

        var own = await _repository.GetByUserIdAsync(currentUserId.Value, cancellationToken);
        return own is null
            ? Array.Empty<EmployeeInformationDto>()
            : new[] { _mapper.Map<EmployeeInformationDto>(own) };
    }
}
