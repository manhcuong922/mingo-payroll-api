using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Features.Contract.Common;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Contract.Commands;

public class UpdateContractCommandHandler : IRequestHandler<UpdateContractCommand, bool>
{
    private readonly IContractRepository _repository;
    private readonly ContractPreflightChecker _preflight;
    private readonly IPayrollStaleMarker _payrollStale;
    private readonly ILogger<UpdateContractCommandHandler> _logger;

    public UpdateContractCommandHandler(
        IContractRepository repository,
        ContractPreflightChecker preflight,
        IPayrollStaleMarker payrollStale,
        ILogger<UpdateContractCommandHandler> logger)
    {
        _repository = repository;
        _preflight = preflight;
        _payrollStale = payrollStale;
        _logger = logger;
    }

    public async Task<bool> Handle(UpdateContractCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, includeAllowances: false, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot update contract {request.Id}: status is {existing.Status}, only Draft can be modified.");

        var (snapshotAmount, snapshotCurrency) = await _preflight.RunAsync(
            new ContractCheckInput(
                EmployeeInformationId: request.EmployeeInformationId,
                EmployerPartyId: request.EmployerPartyId,
                EmployerSignerUserId: request.EmployerSignerUserId,
                PositionId: request.PositionId,
                SalaryGradeId: request.SalaryGradeId,
                EffectiveFrom: request.EffectiveFrom,
                Kind: request.Kind,
                ContractCategory: request.ContractCategory,
                ContractSubtype: request.ContractSubtype,
                HasProbation: request.HasProbation,
                ProbationSalary: request.ProbationSalary,
                AllowanceTypeIds: request.Allowances.Select(a => a.AllowanceTypeId).ToList(),
                ExcludeContractId: request.Id,
                ForceAllowThirdDefinite: request.ForceAllowThirdDefinite),
            cancellationToken);

        var nowUtc = DateTime.UtcNow;
        ContractBuilder.ApplyUpdate(existing, request, snapshotAmount, snapshotCurrency, nowUtc);
        var allowances = ContractBuilder.BuildAllowances(request.Allowances, snapshotCurrency, nowUtc);

        var updated = await _repository.UpdateDraftAsync(existing, allowances, cancellationToken);

        if (updated)
            await TryMarkPayrollStaleAsync(existing, cancellationToken);

        return updated;
    }

    // #66/[D27] — mức lương/phụ cấp/cờ BH của HĐ (kể cả HĐ nháp, vẫn được PayrollDataCollector gom
    // khi không có HĐ Active phủ kỳ) là nguồn tính lương. Sửa HĐ hiệu lực tháng N ⇒ mọi kỳ Draft từ
    // tháng N trở đi cần tính lại. Best-effort — không làm hỏng việc sửa HĐ.
    private async Task TryMarkPayrollStaleAsync(Domain.Entities.Contract contract, CancellationToken ct)
    {
        try
        {
            await _payrollStale.MarkStaleFromMonthAsync(
                contract.EffectiveFrom.Year, contract.EffectiveFrom.Month,
                $"Sửa hợp đồng nháp {contract.ContractNo} (hiệu lực {contract.EffectiveFrom:dd/MM/yyyy})", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Không đánh được cờ stale sau khi sửa HĐ nháp #{ContractId}; việc sửa vẫn thành công.",
                contract.Id);
        }
    }
}
