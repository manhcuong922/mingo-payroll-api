using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Commands;
public class EmployeeInformationDeleteCommandHandler : IRequestHandler<EmployeeInformationDeleteCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public EmployeeInformationDeleteCommandHandler(
        IEmployeeInformationRepository repository,
        ICurrentUserService currentUserService,
        IMediator mediator)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<bool> Handle(EmployeeInformationDeleteCommand request, CancellationToken cancellationToken)
    {
        // Đọc dept trước khi delete — cần cho notification để handler biết dept nào cần gỡ ca.
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        var deleted = await _repository.DeleteAsync(request.Id, cancellationToken);

        // Xử lý như case Leave: notification NewDepartmentId=null → handler gỡ ca dept cũ.
        if (deleted && existing.DepartmentId is int oldDeptId)
        {
            await _mediator.Publish(
                new EmployeeDepartmentChangedNotification(
                    EmployeeInformationId: request.Id,
                    OldDepartmentId: oldDeptId,
                    NewDepartmentId: null,
                    ChangedByUserId: _currentUserService.UserId ?? 0,
                    ChangedAtUtc: DateTime.UtcNow),
                cancellationToken);
        }

        return deleted;
    }
}
