using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Department.Queries;

// Danh sách nhân viên thuộc 1 xưởng. Trả null khi xưởng không tồn tại (controller → 404).
// Scoped: tổ trưởng xưởng (Read.Department) chỉ xem được xưởng mình quản lý, ngược lại → mảng rỗng.
public sealed record DepartmentEmployeesQuery(int DepartmentId)
    : IRequest<IReadOnlyList<EmployeeInformationDto>?>;
