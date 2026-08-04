using FluentValidation;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.SalaryGrade.Validators;

/// <summary>
/// Lương của bậc N phải nằm giữa lương của bậc liền trước và bậc liền sau trong cùng một chức danh.
/// Chỉ so sánh giữa các bậc đang hoạt động và cùng loại tiền tệ (ngoại tệ và VND không so trực tiếp
/// được nếu chưa quy đổi tỷ giá).
/// </summary>
public static class LevelSalaryMonotonicityRule
{
    private const string VndCurrency = "VND";

    public static void AddForCreate(
        AbstractValidator<SalaryGradeCreateCommand> validator,
        ISalaryGradeRepository repository)
    {
        validator.RuleFor(command => command).CustomAsync(async (command, context, ct) =>
        {
            if (command.PositionId <= 0 || command.Level <= 0 || command.BaseSalary <= 0) return;

            var siblings = await repository.GetAllAsync(command.PositionId, isActive: true, ct);
            ValidateMonotonicity(
                context.AddFailure,
                siblings,
                level: command.Level,
                baseSalary: command.BaseSalary,
                currency: command.Currency,
                currentId: null);
        });
    }

    public static void AddForUpdate(
        AbstractValidator<SalaryGradeUpdateCommand> validator,
        ISalaryGradeRepository repository)
    {
        validator.RuleFor(command => command).CustomAsync(async (command, context, ct) =>
        {
            if (command.Id <= 0 || command.BaseSalary <= 0) return;

            var current = await repository.GetByIdAsync(command.Id, ct);
            if (current is null) return;

            var siblings = await repository.GetAllAsync(current.PositionId, isActive: true, ct);
            ValidateMonotonicity(
                context.AddFailure,
                siblings,
                level: current.Level,
                baseSalary: command.BaseSalary,
                currency: command.Currency,
                currentId: current.Id);
        });
    }

    private static void ValidateMonotonicity(
        Action<string, string> addFailure,
        IReadOnlyList<Domain.Entities.SalaryGrade> siblings,
        int level,
        decimal baseSalary,
        string? currency,
        int? currentId)
    {
        var normalizedCurrency = NormalizeCurrency(currency);

        var comparable = siblings
            .Where(s => currentId is null || s.Id != currentId.Value)
            .Where(s => string.Equals(NormalizeCurrency(s.Currency), normalizedCurrency, StringComparison.Ordinal))
            .ToList();

        var lower = comparable
            .Where(s => s.Level < level)
            .OrderByDescending(s => s.Level)
            .FirstOrDefault();
        if (lower is not null && baseSalary <= lower.BaseSalary)
        {
            addFailure(
                nameof(SalaryGradeCreateCommand.BaseSalary),
                $"Lương bậc {level} ({baseSalary:N0} đ) phải lớn hơn lương bậc {lower.Level} ({lower.BaseSalary:N0} đ).");
        }

        var upper = comparable
            .Where(s => s.Level > level)
            .OrderBy(s => s.Level)
            .FirstOrDefault();
        if (upper is not null && baseSalary >= upper.BaseSalary)
        {
            addFailure(
                nameof(SalaryGradeCreateCommand.BaseSalary),
                $"Lương bậc {level} ({baseSalary:N0} đ) phải nhỏ hơn lương bậc {upper.Level} ({upper.BaseSalary:N0} đ).");
        }
    }

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? VndCurrency : currency.Trim().ToUpperInvariant();
}
