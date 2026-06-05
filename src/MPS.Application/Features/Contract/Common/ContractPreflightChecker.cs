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
        // Employee + age ≥ 18 at EffectiveFrom
        var employee = await _lookup.GetUserSnapshotAsync(input.EmployeeId, ct)
            ?? throw new DomainValidationException($"Employee {input.EmployeeId} not found.");

        if (employee.Status != 1)
            throw new DomainValidationException($"Employee {input.EmployeeId} is not active.");

        var ageAtStart = AgeAt(employee.Dob, input.EffectiveFrom);
        if (ageAtStart < LaborLawConstants.SigningAuthority.EmployeeFullCapacityAge)
            throw new DomainValidationException(
                $"EMPLOYEE_UNDER_AGE: Employee must be at least {LaborLawConstants.SigningAuthority.EmployeeFullCapacityAge} at contract EffectiveFrom (current age={ageAtStart}).");

        // EmployerSignerUser exists + active
        if (!await _lookup.IsActiveUserAsync(input.EmployerSignerUserId, ct))
            throw new DomainValidationException($"EmployerSignerUser {input.EmployerSignerUserId} not found or inactive.");

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

        // ContractNo unique
        if (await _contracts.ContractNoExistsAsync(input.ContractNo.Trim(), input.ExcludeContractId, ct))
            throw new DomainValidationException($"ContractNo '{input.ContractNo}' already exists.");

        // Snapshot-derived: probation salary ≥ 85% snapshot
        if (input.HasProbation && input.ProbationSalary is { } probationSalary)
        {
            var minRequired = grade.BaseSalary * (LaborLawConstants.Probation.MinSalaryPercent / 100m);
            if (probationSalary < minRequired)
                throw new DomainValidationException(
                    $"ProbationSalary ({probationSalary}) must be at least {LaborLawConstants.Probation.MinSalaryPercent}% of SalaryAmountSnapshot ({grade.BaseSalary}) — Điều 26 BLLĐ 2019. Minimum: {minRequired:0.##}.");
        }

        // Điều 20 k.2c — không quá 2 HĐ Definite+Official trước đó
        if (input is { ContractCategory: ContractCategory.Definite, ContractSubtype: ContractSubtype.Official }
            && !input.ForceAllowThirdDefinite)
        {
            var count = await _contracts.CountDefiniteOfficialContractsAsync(
                input.EmployeeId, input.EmployerPartyId, input.ExcludeContractId, ct);
            if (count >= 2)
                throw new DomainValidationException(
                    $"Cannot sign a third Definite+Official contract with the same employer (currently {count}). " +
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
    string ContractNo,
    int EmployeeId,
    int EmployerPartyId,
    int EmployerSignerUserId,
    int PositionId,
    int SalaryGradeId,
    DateTime EffectiveFrom,
    ContractCategory ContractCategory,
    ContractSubtype ContractSubtype,
    bool HasProbation,
    decimal? ProbationSalary,
    IReadOnlyList<int> AllowanceTypeIds,
    int? ExcludeContractId,
    bool ForceAllowThirdDefinite);
