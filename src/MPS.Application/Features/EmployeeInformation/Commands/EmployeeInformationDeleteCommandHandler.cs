using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public class EmployeeInformationDeleteCommandHandler : IRequestHandler<EmployeeInformationDeleteCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly IContractRepository _contracts;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public EmployeeInformationDeleteCommandHandler(
        IEmployeeInformationRepository repository,
        IContractRepository contracts,
        ICurrentUserService currentUserService,
        IMediator mediator)
    {
        _repository = repository;
        _contracts = contracts;
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<bool> Handle(EmployeeInformationDeleteCommand request, CancellationToken cancellationToken)
    {
        // #26 — trước đây xoá thẳng, FK từ Contracts nổ ra HTTP 500 và client không biết lý do.
        // Kiểm trước để trả 400 kèm hướng xử lý (phải chấm dứt HĐ trước khi xoá hồ sơ).
        if (await _contracts.HasActiveContractAsync(request.Id, cancellationToken))
            throw new DomainValidationException(
                $"EMPLOYEE_HAS_ACTIVE_CONTRACT: Không thể xoá hồ sơ nhân viên #{request.Id} khi vẫn còn hợp đồng " +
                "hiệu lực. Hãy chấm dứt hoặc huỷ hợp đồng trước.");

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
