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
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public EmployeeInformationGetAllQueryHandler(
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

    public async Task<IReadOnlyList<EmployeeInformationDto>> Handle(
        EmployeeInformationGetAllQuery request,
        CancellationToken cancellationToken)
    {
        // Tầng 1 — Read.All: xem toàn công ty (SystemAdmin, HR, Accountant, Director, DepartmentManager = Ban KH).
        if (_currentUserService.HasPermission(Permissions.EmployeeInformationReadAll))
        {
            var all = await _repository.GetAllAsync(cancellationToken);
            return _mapper.Map<IReadOnlyList<EmployeeInformationDto>>(all);
        }

        var currentUserId = _currentUserService.UserId;
        if (currentUserId is null)
            return Array.Empty<EmployeeInformationDto>();

        // Tầng 2 — Read.Department: quản lý xưởng chỉ xem nhân viên trong xưởng MÌNH TRỰC THUỘC.
        if (_currentUserService.HasPermission(Permissions.EmployeeInformationReadDepartment))
        {
            var myDeptId = await _departmentRepository.GetDepartmentIdOfUserAsync(currentUserId.Value, cancellationToken);
            if (myDeptId is null)
                return Array.Empty<EmployeeInformationDto>();

            var members = await _repository.GetByDepartmentIdAsync(myDeptId.Value, cancellationToken);
            return _mapper.Map<IReadOnlyList<EmployeeInformationDto>>(members);
        }

        // Tầng 3 — không có scope: chỉ hồ sơ của chính mình.
        var own = await _repository.GetByUserIdAsync(currentUserId.Value, cancellationToken);
        return own is null
            ? Array.Empty<EmployeeInformationDto>()
            : new[] { _mapper.Map<EmployeeInformationDto>(own) };
    }
}
