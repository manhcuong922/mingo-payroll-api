using MPS.Application.Features.Contract.Commands;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Tests.TestHelpers;

/// <summary>
/// Produces valid <see cref="CreateContractCommand"/> instances that pass every
/// validator/preflight rule by default. Tests then tweak specific fields with
/// <c>with</c> expressions to isolate the rule under test.
/// </summary>
internal static class ContractCommandFactory
{
    public static CreateContractCommand ValidCreate(
        ContractKind kind = ContractKind.LaborFullTime,
        ContractCategory? category = ContractCategory.Definite,
        ContractSubtype? subtype = ContractSubtype.Official,
        int employeeInformationId = 100,
        int employerPartyId = 1,
        int employerSignerUserId = 1,
        int positionId = 1,
        int salaryGradeId = 1,
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null,
        bool hasProbation = false,
        decimal? probationSalary = null,
        IReadOnlyList<ContractAllowanceInput>? allowances = null,
        bool forceAllowThirdDefinite = false)
    {
        var from = effectiveFrom ?? new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime? to = effectiveTo;

        // For UserChoice kinds the request must carry insurance flags + TaxPolicy; for
        // RuleDriven kinds they must be null (the server derives them from metadata).
        var meta = MPS.Domain.Constants.ContractKindMetadata.Get(kind);
        var isUserChoice = meta.InsuranceMode == InsuranceDeterminationMode.UserChoice;

        // Default EffectiveTo: Definite LaborFullTime → +1 year; any kind that requires
        // EffectiveTo → +1 year; otherwise leave null.
        if (!to.HasValue && (category == ContractCategory.Definite || meta.RequiresEffectiveTo))
            to = from.AddYears(1);

        return new CreateContractCommand(
            Kind: kind,
            ContractCategory: category,
            ContractSubtype: subtype,
            EmployeeInformationId: employeeInformationId,
            EmployerPartyId: employerPartyId,
            EmployerSignerUserId: employerSignerUserId,
            PositionId: positionId,
            JobDescription: "Backend engineer",
            WorkLocation: "Hà Nội",
            EffectiveFrom: from,
            EffectiveTo: to,
            SalaryGradeId: salaryGradeId,
            SalaryPayCycle: SalaryPayCycle.Monthly,
            PromotionPolicy: null,
            WorkingTimeJson: "{\"hoursPerDay\":8,\"daysPerWeek\":5}",
            PpePolicy: null,
            SocialInsuranceFlag: isUserChoice ? false : null,
            HealthInsuranceFlag: isUserChoice ? false : null,
            UnemploymentInsuranceFlag: isUserChoice ? false : null,
            UnionFundExemptFlag: null,   // mặc định: DN vẫn trích KPCĐ
            TaxPolicy: isUserChoice ? TaxPolicy.FlatRate10Percent : null,
            TrainingPolicy: null,
            ConfidentialityClause: null,
            TemplateId: null,
            HasProbation: hasProbation,
            ProbationStartDate: hasProbation ? from : null,
            ProbationEndDate: hasProbation ? from.AddDays(60) : null,
            ProbationSalary: probationSalary ?? (hasProbation ? 17_000_000m : null),
            ProbationResultStatus: hasProbation ? Domain.Enums.Contract.ProbationResultStatus.InProgress : null,
            ParentContractId: null,
            Allowances: allowances ?? Array.Empty<ContractAllowanceInput>(),
            ForceAllowThirdDefinite: forceAllowThirdDefinite);
    }
}
