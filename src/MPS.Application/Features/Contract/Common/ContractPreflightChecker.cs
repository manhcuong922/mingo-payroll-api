using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Contract.Common;

/// <summary>
/// Handler-level validation that requires DB lookups: existence checks, FK validity,
/// employee age, snapshot-derived rules (probation 85%), Điều 20 k.2c rule.
/// Throws <see cref="DomainValidationException"/> on first failure.
/// </summary>
public sealed class ContractPreflightChecker
{
    private readonly IContractLookupReader _lookup;
    private readonly IContractRepository _contracts;

    public ContractPreflightChecker(IContractLookupReader lookup, IContractRepository contracts)
    {
        _lookup = lookup;
        _contracts = contracts;
    }

    public async Task<(decimal SalaryAmountSnapshot, string SalaryCurrencySnapshot)> RunAsync(
        ContractCheckInput input,
        CancellationToken ct)
    {
        // EmployeeInformation existence + age ≥ 18 at EffectiveFrom
        var employeeInfo = await _lookup.GetEmployeeInfoSnapshotAsync(input.EmployeeInformationId, ct)
            ?? throw new DomainValidationException($"EmployeeInformation {input.EmployeeInformationId} not found.");

        var ageAtStart = AgeAt(employeeInfo.Dob, input.EffectiveFrom);
        if (ageAtStart < LaborLawConstants.SigningAuthority.EmployeeFullCapacityAge)
            throw new DomainValidationException(
                $"EMPLOYEE_UNDER_AGE: Employee must be at least {LaborLawConstants.SigningAuthority.EmployeeFullCapacityAge} at contract EffectiveFrom (current age={ageAtStart}).");

        // EmployerSignerUser optional: if provided, must be active.
        // When omitted, the contract document falls back to ContractParty.RepresentativeFullName/Title.
        if (input.EmployerSignerUserId is { } signerId)
        {
            if (!await _lookup.IsActiveUserAsync(signerId, ct))
                throw new DomainValidationException($"EmployerSignerUser {signerId} not found or inactive.");
        }

        // EmployerParty active (EffectiveTo IS NULL) at signing time
        if (!await _lookup.IsActiveContractPartyAsync(input.EmployerPartyId, ct))
            throw new DomainValidationException($"EmployerParty {input.EmployerPartyId} not found or not active.");

        // Position exists
        if (!await _lookup.PositionExistsAsync(input.PositionId, ct))
            throw new DomainValidationException($"Position {input.PositionId} not found.");

        // SalaryGrade IsActive
        var grade = await _lookup.GetSalaryGradeSnapshotAsync(input.SalaryGradeId, ct)
            ?? throw new DomainValidationException($"SalaryGrade {input.SalaryGradeId} not found.");

        if (!grade.IsActive)
            throw new DomainValidationException($"SalaryGrade {input.SalaryGradeId} is inactive.");

        // Allowance types: each active + no duplicates
        if (input.AllowanceTypeIds.Count > 0)
        {
            if (input.AllowanceTypeIds.Distinct().Count() != input.AllowanceTypeIds.Count)
                throw new DomainValidationException("Duplicate AllowanceTypeId in payload.");

            var activeIds = await _lookup.GetActiveAllowanceTypeIdsAsync(input.AllowanceTypeIds, ct);
            var missing = input.AllowanceTypeIds.Except(activeIds).ToList();
            if (missing.Count > 0)
                throw new DomainValidationException(
                    $"AllowanceTypeId(s) not found or inactive: {string.Join(", ", missing)}.");
        }

        // Snapshot-derived: probation salary ≥ 85% snapshot (Điều 26 BLLĐ 2019).
        //
        // ⭐ Áp cho CẢ HAI hình thức thử việc:
        //   (1) thoả thuận thử việc ghi trong HĐLĐ  → `HasProbation = true`;
        //   (2) HỢP ĐỒNG THỬ VIỆC riêng             → `Kind/ContractSubtype = Probation`.
        //
        // Trước đây rule chỉ xét (1), nên hợp đồng thử việc độc lập — hình thức phổ biến hơn — lọt
        // hoàn toàn: không bắt buộc khai `ProbationSalary`, không kiểm 85%, và bảng lương thì trả
        // nguyên 100% lương bậc. Với dạng (2), `ProbationSalary` nay là BẮT BUỘC: hợp đồng thử việc
        // phải ghi rõ mức lương thử việc, và đó cũng là con số duy nhất để bảng lương biết trả bao nhiêu.
        var isProbationContract = input.Kind == ContractKind.Probation
            || input.ContractSubtype == ContractSubtype.Probation;

        if (isProbationContract && input.ProbationSalary is null)
        {
            throw new DomainValidationException(
                "Hợp đồng thử việc phải khai ProbationSalary (mức lương thử việc) — Điều 26 BLLĐ 2019. "
                + $"Mức tối thiểu: {grade.BaseSalary * (LaborLawConstants.Probation.MinSalaryPercent / 100m):0.##} "
                + $"({LaborLawConstants.Probation.MinSalaryPercent}% của {grade.BaseSalary:0.##}).");
        }

        if ((input.HasProbation || isProbationContract) && input.ProbationSalary is { } probationSalary)
        {
            var minRequired = grade.BaseSalary * (LaborLawConstants.Probation.MinSalaryPercent / 100m);
            if (probationSalary < minRequired)
                throw new DomainValidationException(
                    $"ProbationSalary ({probationSalary}) must be at least {LaborLawConstants.Probation.MinSalaryPercent}% of SalaryAmountSnapshot ({grade.BaseSalary}) — Điều 26 BLLĐ 2019. Minimum: {minRequired:0.##}.");
        }

        // Điều 20 k.2c — không quá 2 HĐ Definite+Official trước đó (chỉ áp cho LaborFullTime)
        if (input is { Kind: ContractKind.LaborFullTime, ContractCategory: ContractCategory.Definite, ContractSubtype: ContractSubtype.Official }
            && !input.ForceAllowThirdDefinite)
        {
            var count = await _contracts.CountDefiniteOfficialContractsAsync(
                input.EmployeeInformationId, input.EmployerPartyId, input.ExcludeContractId, ct);
            if (count >= 2)
                throw new DomainValidationException(
                    $"Cannot sign a third Definite+Official LaborFullTime contract with the same employer (currently {count}). " +
                    "Điều 20 k.2c BLLĐ 2019. Use ForceAllowThirdDefinite to override for exempt cases.");
        }

        return (grade.BaseSalary, grade.Currency);
    }

    private static int AgeAt(DateTime dob, DateTime at)
    {
        var age = at.Year - dob.Year;
        if (at < dob.AddYears(age)) age--;
        return age;
    }
}

public sealed record ContractCheckInput(
    int EmployeeInformationId,
    int EmployerPartyId,
    int? EmployerSignerUserId,
    int PositionId,
    int SalaryGradeId,
    DateTime EffectiveFrom,
    ContractKind Kind,
    ContractCategory? ContractCategory,
    ContractSubtype? ContractSubtype,
    bool HasProbation,
    decimal? ProbationSalary,
    IReadOnlyList<int> AllowanceTypeIds,
    int? ExcludeContractId,
    bool ForceAllowThirdDefinite);
