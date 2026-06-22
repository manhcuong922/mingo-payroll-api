using FluentValidation;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.MinimumWage.Commands;
using MPS.Application.Options;
using MPS.Domain.Enums.Compliance;
using Microsoft.Extensions.Options;

namespace MPS.Application.Features.Compliance.MinimumWage.Validators;

public sealed class CreateMinimumWageCommandValidator : AbstractValidator<CreateMinimumWageCommand>
{
    public CreateMinimumWageCommandValidator(IClock clock, IOptions<ComplianceOptions> options)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var maxForward = options.Value.MaxForwardDays;

        When(x => !x.ApplyImmediately, () =>
        {
            RuleFor(x => x.ValidFrom)
                .GreaterThan(today).WithMessage($"ValidFrom must be strictly after today ({today:yyyy-MM-dd}).")
                .LessThanOrEqualTo(today.AddDays(maxForward));
        });

        RuleFor(x => x.LegalReference).MaximumLength(500); // Obsolete — optional, LegalDocumentIds thay thế (BR-15)
        RuleFor(x => x.Regions).NotEmpty();

        RuleFor(x => x.Regions).Custom((regions, ctx) =>
        {
            if (regions is null) return;

            var presentCodes = regions.Select(r => r.RegionCode).ToHashSet();
            foreach (var required in RegionCodes.All)
            {
                if (!presentCodes.Contains(required))
                    ctx.AddFailure("Regions.RegionCode", $"Missing required region: {required}.");
            }

            var seen = new HashSet<string>();
            foreach (var r in regions)
            {
                if (!seen.Add(r.RegionCode))
                    ctx.AddFailure("Regions.RegionCode", $"Duplicate RegionCode: {r.RegionCode}.");
                if (r.AmountPerMonth <= 0)
                    ctx.AddFailure("Regions.AmountPerMonth", $"Region {r.RegionCode} AmountPerMonth must be > 0.");
            }

            // Amount phải giảm dần theo thứ tự RegionCodes.All
            var orderedAmounts = RegionCodes.All
                .Select(code => regions.FirstOrDefault(r => r.RegionCode == code)?.AmountPerMonth)
                .Where(a => a.HasValue)
                .Select(a => a!.Value)
                .ToList();

            for (int i = 1; i < orderedAmounts.Count; i++)
            {
                if (orderedAmounts[i] > orderedAmounts[i - 1])
                    ctx.AddFailure("Regions.AmountPerMonth", $"Region amounts must be in non-increasing order (Region I >= II >= III >= IV).");
            }
        });

        RuleFor(x => x.AppliedRegionCode).NotEmpty();
        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            if (string.IsNullOrWhiteSpace(cmd.AppliedRegionCode) || cmd.Regions is null) return;
            if (!cmd.Regions.Any(r => r.RegionCode == cmd.AppliedRegionCode))
                ctx.AddFailure("AppliedRegionCode", $"AppliedRegionCode '{cmd.AppliedRegionCode}' must match one of the RegionCode values declared in Regions.");
        });

        RuleFor(x => x.LegalDocumentIds).NotEmpty().WithMessage("At least one LegalDocumentId is required (BR-15).");
    }
}

public sealed class UpdateMinimumWageCommandValidator : AbstractValidator<UpdateMinimumWageCommand>
{
    public UpdateMinimumWageCommandValidator(IClock clock)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow);
        RuleFor(x => x.ValidFrom).GreaterThan(today);
        RuleFor(x => x.LegalReference).MaximumLength(500); // Obsolete — optional, LegalDocumentIds thay thế (BR-15)
        RuleFor(x => x.Regions).NotEmpty();

        RuleFor(x => x.AppliedRegionCode).NotEmpty();
        RuleFor(x => x).Custom((cmd, ctx) =>
        {
            if (string.IsNullOrWhiteSpace(cmd.AppliedRegionCode) || cmd.Regions is null) return;
            if (!cmd.Regions.Any(r => r.RegionCode == cmd.AppliedRegionCode))
                ctx.AddFailure("AppliedRegionCode", $"AppliedRegionCode '{cmd.AppliedRegionCode}' must match one of the RegionCode values declared in Regions.");
        });

        RuleFor(x => x.LegalDocumentIds).NotEmpty().WithMessage("At least one LegalDocumentId is required (BR-15).");
    }
}
