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
        var meta = MPS.Domain.Constants.ContractKindMetadata.Get(cmd.Kind);
        var (si, hi, ui, tax) = ResolveInsuranceAndTax(cmd, meta);

        return new Domain.Entities.Contract
        {
            ContractNo = contractNo,
            Kind = cmd.Kind,
            ContractCategory = cmd.ContractCategory,
            ContractSubtype = ResolveContractSubtype(cmd.Kind, cmd.ContractSubtype),
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
            SocialInsuranceFlag = si,
            HealthInsuranceFlag = hi,
            UnemploymentInsuranceFlag = ui,
            TaxPolicy = tax,
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
        var meta = MPS.Domain.Constants.ContractKindMetadata.Get(cmd.Kind);
        var (si, hi, ui, tax) = ResolveInsuranceAndTax(cmd, meta);

        entity.Kind = cmd.Kind;
        entity.ContractCategory = cmd.ContractCategory;
        entity.ContractSubtype = ResolveContractSubtype(cmd.Kind, cmd.ContractSubtype);
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
        entity.SocialInsuranceFlag = si;
        entity.HealthInsuranceFlag = hi;
        entity.UnemploymentInsuranceFlag = ui;
        entity.TaxPolicy = tax;
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

    /// <summary>
    /// Workaround (F3.a): compliance module đang query eligibility bằng ContractSubtype cũ
    /// (Official/Probation). Khi Kind=Probation, tự set ContractSubtype=Probation nếu user
    /// không gửi để rule "eligible if Subtype=Probation" vẫn match. Rename sang
    /// EligibleContractKindsCsv là refactor lâu dài (F3.b), scope PR riêng.
    /// </summary>
    private static Domain.Enums.Contract.ContractSubtype? ResolveContractSubtype(
        Domain.Enums.Contract.ContractKind kind,
        Domain.Enums.Contract.ContractSubtype? requested) =>
        kind == Domain.Enums.Contract.ContractKind.Probation
            ? Domain.Enums.Contract.ContractSubtype.Probation
            : requested;

    /// <summary>
    /// Resolve the 3 insurance flags + TaxPolicy according to the kind's <see cref="Domain.Enums.Contract.InsuranceDeterminationMode"/>.
    /// - RuleDriven: use metadata defaults, ignoring anything from the command.
    /// - UserChoice: use the command's values (validator has already ensured they are non-null).
    /// </summary>
    private static (bool Si, bool Hi, bool Ui, Domain.Enums.Contract.TaxPolicy? Tax) ResolveInsuranceAndTax(
        CreateContractCommand cmd, MPS.Domain.Constants.ContractKindMeta meta) =>
        ResolveInsuranceAndTax(
            cmd.SocialInsuranceFlag, cmd.HealthInsuranceFlag, cmd.UnemploymentInsuranceFlag, cmd.TaxPolicy, meta);

    private static (bool Si, bool Hi, bool Ui, Domain.Enums.Contract.TaxPolicy? Tax) ResolveInsuranceAndTax(
        UpdateContractCommand cmd, MPS.Domain.Constants.ContractKindMeta meta) =>
        ResolveInsuranceAndTax(
            cmd.SocialInsuranceFlag, cmd.HealthInsuranceFlag, cmd.UnemploymentInsuranceFlag, cmd.TaxPolicy, meta);

    private static (bool Si, bool Hi, bool Ui, Domain.Enums.Contract.TaxPolicy? Tax) ResolveInsuranceAndTax(
        bool? si, bool? hi, bool? ui, Domain.Enums.Contract.TaxPolicy? tax,
        MPS.Domain.Constants.ContractKindMeta meta)
    {
        if (meta.InsuranceMode == Domain.Enums.Contract.InsuranceDeterminationMode.RuleDriven)
        {
            return (
                meta.DefaultSocialInsurance ?? false,
                meta.DefaultHealthInsurance ?? false,
                meta.DefaultUnemploymentInsurance ?? false,
                meta.DefaultTaxPolicy);
        }

        // UserChoice — validator guarantees non-null; fall back to false / null defensively.
        return (si ?? false, hi ?? false, ui ?? false, tax);
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
