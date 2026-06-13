using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.PitRegulation.Commands;
using MPS.Application.Options;
using Microsoft.Extensions.Options;

namespace MPS.Application.Features.Compliance.PitRegulation.Validators;

public sealed class CreatePitRegulationCommandValidator : AbstractValidator<CreatePitRegulationCommand>
{
    public CreatePitRegulationCommandValidator(IClock clock, IOptions<ComplianceOptions> options)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var maxForward = options.Value.MaxForwardDays;

        When(x => !x.ApplyImmediately, () =>
        {
            RuleFor(x => x.ValidFrom)
                .GreaterThan(today).WithMessage($"ValidFrom must be strictly after today ({today:yyyy-MM-dd}).")
                .LessThanOrEqualTo(today.AddDays(maxForward));
        });

        RuleFor(x => x.PersonalDeductionAmount).GreaterThan(0);
        RuleFor(x => x.DependentDeductionAmount).GreaterThan(0);
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Brackets).NotEmpty().WithMessage("At least one PIT bracket is required.");

        RuleFor(x => x.Brackets).Custom((brackets, ctx) =>
        {
            if (brackets is null || brackets.Count == 0) return;

            var ordered = brackets.OrderBy(b => b.Order).ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var b = ordered[i];
                if (b.Order != i + 1)
                {
                    ctx.AddFailure("Brackets.Order", $"Bracket Order must be consecutive starting from 1. Found gap at position {i + 1}.");
                    return;
                }
                if (b.Rate < 0 || b.Rate > 1)
                    ctx.AddFailure("Brackets.Rate", $"Bracket Order={b.Order} has invalid Rate {b.Rate}. Must be between 0 and 1.");
                if (i == 0 && b.LowerBound != 0)
                    ctx.AddFailure("Brackets.LowerBound", "First bracket LowerBound must be 0.");
                if (i < ordered.Count - 1)
                {
                    if (ordered[i].UpperBound is null)
                        ctx.AddFailure("Brackets.UpperBound", $"Bracket Order={b.Order} is not the last but has null UpperBound.");
                    if (ordered[i].UpperBound.HasValue && ordered[i + 1].LowerBound != ordered[i].UpperBound!.Value)
                        ctx.AddFailure("Brackets.LowerBound", $"Bracket Order={i + 2} LowerBound must equal previous UpperBound.");
                    if (i > 0 && b.Rate < ordered[i - 1].Rate)
                        ctx.AddFailure("Brackets.Rate", $"Bracket Order={b.Order} Rate must be >= previous bracket Rate (ascending).");
                }
                else if (b.UpperBound is not null)
                {
                    ctx.AddFailure("Brackets.UpperBound", "Last bracket UpperBound must be null (unbounded).");
                }
            }
        });
    }
}

public sealed class UpdatePitRegulationCommandValidator : AbstractValidator<UpdatePitRegulationCommand>
{
    public UpdatePitRegulationCommandValidator(IClock clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        RuleFor(x => x.ValidFrom).GreaterThan(today);
        RuleFor(x => x.PersonalDeductionAmount).GreaterThan(0);
        RuleFor(x => x.DependentDeductionAmount).GreaterThan(0);
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Brackets).NotEmpty();
    }
}
