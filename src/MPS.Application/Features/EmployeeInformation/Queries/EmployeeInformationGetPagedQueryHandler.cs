using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Queries;

public class EmployeeInformationGetPagedQueryHandler
    : IRequestHandler<EmployeeInformationGetPagedQuery, PagedResult<EmployeeInformationDto>>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public EmployeeInformationGetPagedQueryHandler(
        IEmployeeInformationRepository repository,
        IMapper mapper,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _mapper = mapper;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<EmployeeInformationDto>> Handle(
        EmployeeInformationGetPagedQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasPermission(Permissions.EmployeeInformationReadAll))
        {
            var currentUserId = _currentUserService.UserId;
            if (currentUserId is null)
                return new PagedResult<EmployeeInformationDto>(
                    Array.Empty<EmployeeInformationDto>(), 0, request.Page, request.PageSize);

            var own = await _repository.GetByUserIdAsync(currentUserId.Value, cancellationToken);
            if (own is null)
                return new PagedResult<EmployeeInformationDto>(
                    Array.Empty<EmployeeInformationDto>(), 0, request.Page, request.PageSize);

            var ownDto = _mapper.Map<EmployeeInformationDto>(own);
            return new PagedResult<EmployeeInformationDto>(
                new[] { ownDto }, 1, request.Page, request.PageSize);
        }

        var (items, total) = await _repository.GetPagedAsync(
            request.Keyword,
            request.PositionId,
            request.HasAccount,
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<EmployeeInformationDto>>(items);
        return new PagedResult<EmployeeInformationDto>(dtos, total, request.Page, request.PageSize);
    }
}
