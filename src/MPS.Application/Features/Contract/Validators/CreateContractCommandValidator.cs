using FluentValidation;
using MPS.Application.Features.Contract.Commands;

namespace MPS.Application.Features.Contract.Validators;

public class CreateContractCommandValidator : AbstractValidator<CreateContractCommand>
{
    public CreateContractCommandValidator()
    {
        ContractValidationRules.ApplyCommonRules(this, cmd => new ContractCore(
            cmd.Kind,
            cmd.ContractCategory,
            cmd.ContractSubtype,
            cmd.EmployeeInformationId,
            cmd.EmployerPartyId,
            cmd.EmployerSignerUserId,
            cmd.PositionId,
            cmd.SalaryGradeId,
            cmd.JobDescription,
            cmd.WorkLocation,
            cmd.EffectiveFrom,
            cmd.EffectiveTo,
            cmd.WorkingTimeJson,
            cmd.SocialInsuranceFlag,
            cmd.HealthInsuranceFlag,
            cmd.UnemploymentInsuranceFlag,
            cmd.TaxPolicy,
            cmd.HasProbation,
            cmd.ProbationStartDate,
            cmd.ProbationEndDate,
            cmd.ProbationSalary,
            cmd.ProbationResultStatus,
            cmd.Allowances));
    }
}
