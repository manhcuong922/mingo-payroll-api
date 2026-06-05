using FluentValidation;
using MPS.Application.Features.Contract.Commands;

namespace MPS.Application.Features.Contract.Validators;

public class UpdateContractCommandValidator : AbstractValidator<UpdateContractCommand>
{
    public UpdateContractCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        ContractValidationRules.ApplyCommonRules(this, cmd => new ContractCore(
            cmd.ContractNo,
            cmd.ContractCategory,
            cmd.ContractSubtype,
            cmd.Form,
            cmd.EmployeeId,
            cmd.EmployerPartyId,
            cmd.EmployerSignerUserId,
            cmd.PositionId,
            cmd.SalaryGradeId,
            cmd.JobDescription,
            cmd.WorkLocation,
            cmd.EffectiveFrom,
            cmd.EffectiveTo,
            cmd.WorkingTimeJson,
            cmd.ContractText,
            cmd.HasProbation,
            cmd.ProbationStartDate,
            cmd.ProbationEndDate,
            cmd.ProbationSalary,
            cmd.ProbationResultStatus,
            cmd.Allowances));
    }
}
