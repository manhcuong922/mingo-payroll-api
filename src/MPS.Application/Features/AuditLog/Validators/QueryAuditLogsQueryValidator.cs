using FluentValidation;
using MPS.Application.Features.AuditLog.Queries.QueryAuditLogs;

namespace MPS.Application.Features.AuditLog.Validators;

public class QueryAuditLogsQueryValidator : AbstractValidator<QueryAuditLogsQuery>
{
    private const int MaxRangeDays = 366;
    private const int MaxPageSize = 50;

    public QueryAuditLogsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.");

        RuleFor(x => x.Size)
            .InclusiveBetween(1, MaxPageSize)
            .WithMessage($"Size must be between 1 and {MaxPageSize}.");

        RuleFor(x => x)
            .Must(HaveValidDateRange)
            .WithMessage("FromUtc must be less than or equal to ToUtc.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);

        RuleFor(x => x)
            .Must(BeWithinMaxRange)
            .WithMessage($"Date range must not exceed {MaxRangeDays} days.")
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue);
    }

    private static bool HaveValidDateRange(QueryAuditLogsQuery q) =>
        q.FromUtc!.Value <= q.ToUtc!.Value;

    private static bool BeWithinMaxRange(QueryAuditLogsQuery q) =>
        (q.ToUtc!.Value - q.FromUtc!.Value).TotalDays <= MaxRangeDays;
}
