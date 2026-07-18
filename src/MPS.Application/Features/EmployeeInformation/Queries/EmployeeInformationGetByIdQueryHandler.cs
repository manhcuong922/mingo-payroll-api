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
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public EmployeeInformationGetByIdQueryHandler(
        IEmployeeInformationRepository repository,
        IDepartmentRepository departmentRepository,
        IMapper mapper,
        ICurrentUserService currentUserService)
    {
        _repository = repository;
        _departmentRepository = departmentRepository;
        _mapper = mapper;
        _currentUserService = currentUserService;
    }

    public async Task<EmployeeInformationDto?> Handle(
        EmployeeInformationGetByIdQuery request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (entity is null) return null;

        // Read.All → xem bất kỳ ai.
        if (_currentUserService.HasPermission(Permissions.EmployeeInformationReadAll))
            return _mapper.Map<EmployeeInformationDto>(entity);

        // Chính mình → luôn xem được.
        if (entity.UserId == _currentUserService.UserId)
            return _mapper.Map<EmployeeInformationDto>(entity);

        // Read.Department → xem được nhân viên thuộc CÙNG xưởng mình trực thuộc.
        var currentUserId = _currentUserService.UserId;
        if (currentUserId is not null
            && _currentUserService.HasPermission(Permissions.EmployeeInformationReadDepartment)
            && entity.DepartmentId is not null)
        {
            var myDeptId = await _departmentRepository.GetDepartmentIdOfUserAsync(currentUserId.Value, cancellationToken);
            if (myDeptId is not null && myDeptId.Value == entity.DepartmentId)
                return _mapper.Map<EmployeeInformationDto>(entity);
        }

        throw new ForbiddenAccessException("You can only view your own employee information.");
    }
}
