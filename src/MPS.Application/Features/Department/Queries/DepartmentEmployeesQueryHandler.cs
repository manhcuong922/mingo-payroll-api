using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Domain.Constants;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Queries;

public class DepartmentEmployeesQueryHandler
    : IRequestHandler<DepartmentEmployeesQuery, IReadOnlyList<EmployeeInformationDto>?>
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IMapper _mapper;
    private readonly ICurrentUserService _currentUserService;

    public DepartmentEmployeesQueryHandler(
        IDepartmentRepository departmentRepository,
        IEmployeeInformationRepository employeeRepository,
        IMapper mapper,
        ICurrentUserService currentUserService)
    {
        _departmentRepository = departmentRepository;
        _employeeRepository = employeeRepository;
        _mapper = mapper;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<EmployeeInformationDto>?> Handle(
        DepartmentEmployeesQuery request, CancellationToken cancellationToken)
    {
        var department = await _departmentRepository.GetByIdAsync(request.DepartmentId, cancellationToken);
        if (department is null)
            return null;

        // Read.All: xem nhân viên của bất kỳ xưởng nào.
        if (_currentUserService.HasPermission(Permissions.EmployeeInformationReadAll))
            return await MapMembersAsync(request.DepartmentId, cancellationToken);

        // Read.Department: chỉ xem được xưởng MÌNH TRỰC THUỘC; xưởng khác → mảng rỗng (không lộ dữ liệu).
        if (_currentUserService.HasPermission(Permissions.EmployeeInformationReadDepartment))
        {
            var currentUserId = _currentUserService.UserId;
            if (currentUserId is null)
                return Array.Empty<EmployeeInformationDto>();

            var myDeptId = await _departmentRepository.GetDepartmentIdOfUserAsync(currentUserId.Value, cancellationToken);
            if (myDeptId is null || myDeptId.Value != request.DepartmentId)
                return Array.Empty<EmployeeInformationDto>();

            return await MapMembersAsync(request.DepartmentId, cancellationToken);
        }

        return Array.Empty<EmployeeInformationDto>();
    }

    private async Task<IReadOnlyList<EmployeeInformationDto>> MapMembersAsync(int departmentId, CancellationToken ct)
    {
        var members = await _employeeRepository.GetByDepartmentIdAsync(departmentId, ct);
        return _mapper.Map<IReadOnlyList<EmployeeInformationDto>>(members);
    }
}
