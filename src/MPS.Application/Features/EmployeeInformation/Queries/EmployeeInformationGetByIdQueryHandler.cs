using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Queries;

public class EmployeeInformationGetByIdQueryHandler
    : IRequestHandler<EmployeeInformationGetByIdQuery, EmployeeInformationDto?>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public EmployeeInformationGetByIdQueryHandler(
        IEmployeeInformationRepository repository,
        IMapper mapper,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _mapper = mapper;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeInformationDto?> Handle(
        EmployeeInformationGetByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        if (!_currentUserService.HasPermission(Permissions.EmployeeInformationReadAll)
            && entity.UserId != _currentUserService.UserId)
        {
            throw new ForbiddenAccessException("You can only view your own employee information.");
        }

        return _mapper.Map<EmployeeInformationDto>(entity);
    }
}
