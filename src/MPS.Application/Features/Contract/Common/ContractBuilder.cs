using MPS.Application.Features.Contract.Commands;
using MPS.Domain.Entities;

namespace MPS.Application.Features.Contract.Common;

/// <summary>
/// Pure mapping from command record → entity. Snapshot fields (SalaryAmountSnapshot,
/// SalaryCurrencySnapshot) must be supplied by caller (handler reads from SalaryGrade).
/// Form and SalaryFormType are hardcoded: Written and Time (only values used by Mingo).
/// </summary>
internal static class ContractBuilder
{
    public static Domain.Entities.Contract FromCreate(
        CreateContractCommand cmd,
        string contractNo,
        decimal salaryAmountSnapshot,
        string salaryCurrencySnapshot,
        int createdByUserId,
        DateTime nowUtc)
    {
        return new Domain.Entities.Contract
        {
            ContractNo = contractNo,
            ContractCategory = cmd.ContractCategory,
            ContractSubtype = cmd.ContractSubtype,
            Form = Domain.Enums.Contract.ContractForm.Written,
            EmployeeInformationId = cmd.EmployeeInformationId,
            EmployerPartyId = cmd.EmployerPartyId,
            EmployerSignerUserId = cmd.EmployerSignerUserId,
            PositionId = cmd.PositionId,
            JobDescription = cmd.JobDescription,
            WorkLocation = cmd.WorkLocation,
            EffectiveFrom = cmd.EffectiveFrom,
            EffectiveTo = cmd.EffectiveTo,
            SalaryGradeId = cmd.SalaryGradeId,
            SalaryAmountSnapshot = salaryAmountSnapshot,
            SalaryCurrencySnapshot = salaryCurrencySnapshot,
            SalaryFormType = Domain.Enums.Contract.SalaryFormType.Time,
            SalaryPayCycle = cmd.SalaryPayCycle,
            PromotionPolicy = cmd.PromotionPolicy,
            WorkingTimeJson = cmd.WorkingTimeJson,
            PpePolicy = cmd.PpePolicy,
            SocialInsuranceFlag = cmd.SocialInsuranceFlag,
            HealthInsuranceFlag = cmd.HealthInsuranceFlag,
            UnemploymentInsuranceFlag = cmd.UnemploymentInsuranceFlag,
            TrainingPolicy = cmd.TrainingPolicy,
            ConfidentialityClause = cmd.ConfidentialityClause,
            TemplateId = cmd.TemplateId,
            Status = Domain.Enums.Contract.ContractStatus.Draft,
            ParentContractId = cmd.ParentContractId,
            ConvertedFromDefiniteFlag = false,
            HasProbation = cmd.HasProbation,
            ProbationStartDate = cmd.ProbationStartDate,
            ProbationEndDate = cmd.ProbationEndDate,
            ProbationSalary = cmd.ProbationSalary,
            ProbationResultStatus = cmd.ProbationResultStatus,
            ProbationResultNotifiedAt = null,
            ProbationCancelledByParty = null,
            CreatedAt = nowUtc,
            CreatedByUserId = createdByUserId,
        };
    }

    public static void ApplyUpdate(
        Domain.Entities.Contract entity,
        UpdateContractCommand cmd,
        decimal salaryAmountSnapshot,
        string salaryCurrencySnapshot,
        DateTime nowUtc)
    {
        entity.ContractCategory = cmd.ContractCategory;
        entity.ContractSubtype = cmd.ContractSubtype;
        entity.Form = Domain.Enums.Contract.ContractForm.Written;
        entity.EmployeeInformationId = cmd.EmployeeInformationId;
        entity.EmployerPartyId = cmd.EmployerPartyId;
        entity.EmployerSignerUserId = cmd.EmployerSignerUserId;
        entity.PositionId = cmd.PositionId;
        entity.JobDescription = cmd.JobDescription;
        entity.WorkLocation = cmd.WorkLocation;
        entity.EffectiveFrom = cmd.EffectiveFrom;
        entity.EffectiveTo = cmd.EffectiveTo;
        entity.SalaryGradeId = cmd.SalaryGradeId;
        entity.SalaryAmountSnapshot = salaryAmountSnapshot;
        entity.SalaryCurrencySnapshot = salaryCurrencySnapshot;
        entity.SalaryFormType = Domain.Enums.Contract.SalaryFormType.Time;
        entity.SalaryPayCycle = cmd.SalaryPayCycle;
        entity.PromotionPolicy = cmd.PromotionPolicy;
        entity.WorkingTimeJson = cmd.WorkingTimeJson;
        entity.PpePolicy = cmd.PpePolicy;
        entity.SocialInsuranceFlag = cmd.SocialInsuranceFlag;
        entity.HealthInsuranceFlag = cmd.HealthInsuranceFlag;
        entity.UnemploymentInsuranceFlag = cmd.UnemploymentInsuranceFlag;
        entity.TrainingPolicy = cmd.TrainingPolicy;
        entity.ConfidentialityClause = cmd.ConfidentialityClause;
        entity.TemplateId = cmd.TemplateId;
        entity.ParentContractId = cmd.ParentContractId;
        entity.HasProbation = cmd.HasProbation;
        entity.ProbationStartDate = cmd.ProbationStartDate;
        entity.ProbationEndDate = cmd.ProbationEndDate;
        entity.ProbationSalary = cmd.ProbationSalary;
        entity.ProbationResultStatus = cmd.ProbationResultStatus;
        entity.UpdatedAt = nowUtc;
    }

    public static IEnumerable<ContractAllowance> BuildAllowances(
        IReadOnlyList<ContractAllowanceInput> inputs,
        string defaultCurrency,
        DateTime nowUtc) =>
        inputs.Select(i => new ContractAllowance
        {
            AllowanceTypeId = i.AllowanceTypeId,
            Amount = i.Amount,
            Currency = string.IsNullOrWhiteSpace(i.Currency) ? defaultCurrency : i.Currency.Trim().ToUpperInvariant(),
            Note = i.Note?.Trim(),
            CreatedAt = nowUtc,
        });
}
