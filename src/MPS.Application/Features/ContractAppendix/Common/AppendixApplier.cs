using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractAppendix.Common;

/// <summary>
/// Apply một <see cref="AppendixChangeSet"/> đã validate lên 1 Contract đang tracked
/// (kèm collection ContractAllowances cũng tracked). Mọi FK (SalaryGrade, AllowanceType)
/// được verify còn active trước khi áp dụng — không tin tưởng client.
///
/// <para>
/// Service này phải được gọi <b>trong</b> transaction của
/// <see cref="IContractAppendixRepository.ActivateDraftAsync"/> để mọi mutation đều atomic.
/// </para>
/// </summary>
public sealed class AppendixApplier
{
    private readonly IContractLookupReader _lookup;

    public AppendixApplier(IContractLookupReader lookup)
    {
        _lookup = lookup;
    }

    /// <summary>
    /// Apply tại chỗ. Caller chịu trách nhiệm save changes / commit tx.
    /// </summary>
    public async Task ApplyAsync(
        Domain.Entities.Contract contract,
        ICollection<ContractAllowance> contractAllowances,
        AppendixChangeSet change,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(change);

        if (change.JobDescription is not null)
            contract.JobDescription = change.JobDescription.Trim();

        if (change.WorkLocation is not null)
            contract.WorkLocation = change.WorkLocation.Trim();

        if (change.PromotionPolicy is not null)
            contract.PromotionPolicy = change.PromotionPolicy;

        if (change.PpePolicy is not null)
            contract.PpePolicy = change.PpePolicy;

        if (change.TrainingPolicy is not null)
            contract.TrainingPolicy = change.TrainingPolicy;

        if (change.ConfidentialityClause is not null)
            contract.ConfidentialityClause = change.ConfidentialityClause;

        if (change.WorkingTimeJson is not null)
            contract.WorkingTimeJson = change.WorkingTimeJson;

        // SalaryGrade swap: chỉ đổi reference, KHÔNG re-freeze SalaryAmountSnapshot
        // (snapshot giữ giá trị tại lần ký gốc; phụ lục tự ghi nhận giá trị mới trong PDF).
        if (change.SalaryGradeId is { } newGradeId && newGradeId != contract.SalaryGradeId)
        {
            var grade = await _lookup.GetSalaryGradeSnapshotAsync(newGradeId, cancellationToken)
                ?? throw new DomainValidationException($"SalaryGrade {newGradeId} not found.");
            if (!grade.IsActive)
                throw new DomainValidationException($"SalaryGrade {newGradeId} is inactive.");
            contract.SalaryGradeId = newGradeId;
        }

        await ApplyAllowanceChangesAsync(contract, contractAllowances, change, nowUtc, cancellationToken);

        contract.UpdatedAt = nowUtc;
    }

    private async Task ApplyAllowanceChangesAsync(
        Domain.Entities.Contract contract,
        ICollection<ContractAllowance> contractAllowances,
        AppendixChangeSet change,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // 1. Remove: chỉ xóa row đang tồn tại; bỏ qua id không match (idempotent — phụ lục
        //    có thể remove cùng id 2 lần qua các phụ lục khác nhau, không crash).
        if (change.RemoveAllowanceTypeIds.Count > 0)
        {
            var toRemove = contractAllowances
                .Where(a => change.RemoveAllowanceTypeIds.Contains(a.AllowanceTypeId))
                .ToList();
            foreach (var allowance in toRemove)
                contractAllowances.Remove(allowance);
        }

        // 2. Update: amount/currency/note. Throw nếu type chưa tồn tại — caller phải dùng Add.
        foreach (var upd in change.UpdateAllowances)
        {
            var existing = contractAllowances.FirstOrDefault(a => a.AllowanceTypeId == upd.AllowanceTypeId)
                ?? throw new DomainValidationException(
                    $"Cannot update allowance type {upd.AllowanceTypeId}: not present on contract.");

            existing.Amount = upd.Amount;
            existing.Currency = NormalizeCurrency(upd.Currency, contract.SalaryCurrencySnapshot);
            existing.Note = upd.Note?.Trim();
        }

        // 3. Add: validate AllowanceType còn active + chưa tồn tại trên HĐ.
        if (change.AddAllowances.Count == 0) return;

        var addIds = change.AddAllowances.Select(a => a.AllowanceTypeId).ToList();
        if (addIds.Distinct().Count() != addIds.Count)
            throw new DomainValidationException("Duplicate AllowanceTypeId in AddAllowances.");

        var activeIds = await _lookup.GetActiveAllowanceTypeIdsAsync(addIds, cancellationToken);
        var missing = addIds.Except(activeIds).ToList();
        if (missing.Count > 0)
            throw new DomainValidationException(
                $"AllowanceTypeId(s) not found or inactive: {string.Join(", ", missing)}.");

        foreach (var add in change.AddAllowances)
        {
            if (contractAllowances.Any(a => a.AllowanceTypeId == add.AllowanceTypeId))
                throw new DomainValidationException(
                    $"AllowanceTypeId {add.AllowanceTypeId} already exists on contract — use UpdateAllowances instead.");

            contractAllowances.Add(new ContractAllowance
            {
                ContractId = contract.Id,
                AllowanceTypeId = add.AllowanceTypeId,
                Amount = add.Amount,
                Currency = NormalizeCurrency(add.Currency, contract.SalaryCurrencySnapshot),
                Note = add.Note?.Trim(),
                CreatedAt = nowUtc,
            });
        }
    }

    private static string NormalizeCurrency(string? supplied, string fallback) =>
        string.IsNullOrWhiteSpace(supplied) ? fallback : supplied.Trim().ToUpperInvariant();
}
