using FluentValidation;
using MPS.Application.Features.Compliance.AllowanceExemptThreshold.Commands;
using MPS.Domain.Enums.Compliance;

namespace MPS.Application.Features.Compliance.AllowanceExemptThreshold.Validators;

public sealed class CreateAllowanceExemptThresholdCommandValidator : AbstractValidator<CreateAllowanceExemptThresholdCommand>
{
    public CreateAllowanceExemptThresholdCommandValidator()
    {
        RuleFor(x => x.AllowanceTypeId).GreaterThan(0);
        RuleFor(x => x.ThresholdMode).Must(m => ThresholdModes.All.Contains(m))
            .WithMessage($"ThresholdMode must be one of: {string.Join(", ", ThresholdModes.All)}.");
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);

        When(x => x.ThresholdMode == ThresholdModes.FixedAmount, () =>
            RuleFor(x => x.MonthlyExemptAmount).NotNull().GreaterThan(0)
                .WithMessage("MonthlyExemptAmount must be > 0 when ThresholdMode is FixedAmount."));

        When(x => x.ThresholdMode == ThresholdModes.ActualAllocation, () =>
            RuleFor(x => x.MonthlyExemptAmount).Null()
                .WithMessage("MonthlyExemptAmount must be null when ThresholdMode is ActualAllocation."));

        RuleFor(x => x).Must(x => x.AppliesToTax || x.AppliesToSocial)
            .WithMessage("At least one of AppliesToTax or AppliesToSocial must be true.");
    }
}

public sealed class UpdateAllowanceExemptThresholdCommandValidator : AbstractValidator<UpdateAllowanceExemptThresholdCommand>
{
    public UpdateAllowanceExemptThresholdCommandValidator()
    {
        RuleFor(x => x.ThresholdMode).Must(m => ThresholdModes.All.Contains(m))
            .WithMessage($"ThresholdMode must be one of: {string.Join(", ", ThresholdModes.All)}.");
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);

        When(x => x.ThresholdMode == ThresholdModes.FixedAmount, () =>
            RuleFor(x => x.MonthlyExemptAmount).NotNull().GreaterThan(0));

        When(x => x.ThresholdMode == ThresholdModes.ActualAllocation, () =>
            RuleFor(x => x.MonthlyExemptAmount).Null());

        RuleFor(x => x).Must(x => x.AppliesToTax || x.AppliesToSocial)
            .WithMessage("At least one of AppliesToTax or AppliesToSocial must be true.");
    }
}
