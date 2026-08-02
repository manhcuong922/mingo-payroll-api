using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Department.Commands;

public class DepartmentRemoveEmployeeCommandHandler
    : IRequestHandler<DepartmentRemoveEmployeeCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public DepartmentRemoveEmployeeCommandHandler(
        IEmployeeInformationRepository repository,
        ICurrentUserService currentUserService,
        IMediator mediator)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<bool> Handle(DepartmentRemoveEmployeeCommand request, CancellationToken cancellationToken)
    {
        var employee = await _repository.GetByIdAsync(request.EmployeeInformationId, cancellationToken);
        if (employee is null) return false;

        // Không cho gỡ nhầm qua id xưởng khác — nếu hồ sơ không thuộc xưởng đang thao tác thì đây
        // là request sai, báo rõ thay vì lặng lẽ set null.
        if (employee.DepartmentId != request.DepartmentId)
        {
            throw new DomainValidationException(
                $"Nhân viên #{request.EmployeeInformationId} không thuộc xưởng #{request.DepartmentId}.");
        }

        var removed = await _repository.SetDepartmentAsync(request.EmployeeInformationId, null, cancellationToken);
        if (!removed) return false;

        // Rời xưởng ⇒ gỡ ca do lịch của xưởng cũ sinh ra (handler AutoApplySchedule xử lý).
        await _mediator.Publish(
            new EmployeeDepartmentChangedNotification(
                EmployeeInformationId: request.EmployeeInformationId,
                OldDepartmentId: request.DepartmentId,
                NewDepartmentId: null,
                ChangedByUserId: _currentUserService.UserId ?? 0,
                ChangedAtUtc: DateTime.UtcNow),
            cancellationToken);

        return true;
    }
}
