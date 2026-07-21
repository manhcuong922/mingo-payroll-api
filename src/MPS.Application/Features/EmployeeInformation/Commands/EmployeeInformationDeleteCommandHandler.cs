using MediatR;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public class EmployeeInformationDeleteCommandHandler : IRequestHandler<EmployeeInformationDeleteCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly IContractRepository _contracts;

    public EmployeeInformationDeleteCommandHandler(
        IEmployeeInformationRepository repository,
        IContractRepository contracts)
    {
        _repository = repository;
        _contracts = contracts;
    }

    public async Task<bool> Handle(EmployeeInformationDeleteCommand request, CancellationToken cancellationToken)
    {
        // #26 — trước đây xoá thẳng, FK từ Contracts nổ ra HTTP 500 và client không biết lý do.
        // Kiểm trước để trả 400 kèm hướng xử lý (phải chấm dứt HĐ trước khi xoá hồ sơ).
        if (await _contracts.HasActiveContractAsync(request.Id, cancellationToken))
            throw new DomainValidationException(
                $"EMPLOYEE_HAS_ACTIVE_CONTRACT: Không thể xoá hồ sơ nhân viên #{request.Id} khi vẫn còn hợp đồng " +
                "hiệu lực. Hãy chấm dứt hoặc huỷ hợp đồng trước.");

        return await _repository.DeleteAsync(request.Id, cancellationToken);
    }
}
