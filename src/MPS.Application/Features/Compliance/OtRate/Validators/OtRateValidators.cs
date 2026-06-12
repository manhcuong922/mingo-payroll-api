using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.OtRate.Commands;
using MPS.Application.Options;
using MPS.Domain.Enums.Compliance;
using Microsoft.Extensions.Options;

namespace MPS.Application.Features.Compliance.OtRate.Validators;

public sealed class CreateOtRateCommandValidator : AbstractValidator<CreateOtRateCommand>
{
    public CreateOtRateCommandValidator(IClock clock, IOptions<ComplianceOptions> options)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var maxForward = options.Value.MaxForwardDays;

        When(x => !x.ApplyImmediately, () =>
        {
            RuleFor(x => x.ValidFrom)
                .GreaterThan(today).WithMessage($"ValidFrom must be strictly after today ({today:yyyy-MM-dd}).")
                .LessThanOrEqualTo(today.AddDays(maxForward));
        });

        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Entries).NotEmpty();

        RuleFor(x => x.Entries).Custom((entries, ctx) =>
        {
            if (entries is null) return;

            var presentTypes = entries.Select(e => e.OtType).ToHashSet();
            foreach (var required in OtRateTypes.Required)
            {
                if (!presentTypes.Contains(required))
                    ctx.AddFailure("Entries.OtType", $"Missing required OT type: {required}.");
            }

            var seen = new HashSet<string>();
            foreach (var e in entries)
            {
                if (!seen.Add(e.OtType))
                    ctx.AddFailure("Entries.OtType", $"Duplicate OtType: {e.OtType}.");

                if (OtRateTypes.MinimumRates.TryGetValue(e.OtType, out var floor) && e.Rate < floor)
                    ctx.AddFailure("Entries.Rate", $"OtType={e.OtType} Rate {e.Rate} is below legal minimum {floor}.");

                if (e.Rate > 10)
                    ctx.AddFailure("Entries.Rate", $"OtType={e.OtType} Rate {e.Rate} exceeds sanity cap of 10.");
            }
        });
    }
}

public sealed class UpdateOtRateCommandValidator : AbstractValidator<UpdateOtRateCommand>
{
    public UpdateOtRateCommandValidator(IClock clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        RuleFor(x => x.ValidFrom).GreaterThan(today);
        RuleFor(x => x.LegalReference).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Entries).NotEmpty();
    }
}
