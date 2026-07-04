using System.Globalization;
using System.Text;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Common;

internal static class ShiftTemplateCodeGenerator
{
    private const int MaxCodeLength = 20;

    public static async Task<string> GenerateUniqueAsync(
        string name,
        IShiftTemplateRepository repository,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var slug = BuildSlug(name);
        var baseCode = BuildCandidate(slug, suffix: null);
        if (!await repository.ExistsByCodeAsync(baseCode, excludeId, cancellationToken))
        {
            return baseCode;
        }

        for (var sequence = 2; sequence <= 999; sequence++)
        {
            var candidate = BuildCandidate(slug, sequence.ToString(CultureInfo.InvariantCulture));
            if (!await repository.ExistsByCodeAsync(candidate, excludeId, cancellationToken))
            {
                return candidate;
            }
        }

        throw new ConflictException($"Không thể tạo mã ca duy nhất từ tên '{name}'.");
    }

    private static string BuildSlug(string name)
    {
        var normalized = name.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingSeparator = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var mapped = character is 'đ' or 'Đ'
                ? 'D'
                : char.ToUpperInvariant(character);
            if (char.IsLetterOrDigit(mapped))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }
                builder.Append(mapped);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "LAM-VIEC" : slug;
    }

    private static string BuildCandidate(string slug, string? suffix)
    {
        var suffixPart = string.IsNullOrEmpty(suffix) ? string.Empty : $"-{suffix}";
        var maxSlugLength = MaxCodeLength - suffixPart.Length;
        var trimmedSlug = slug.Length <= maxSlugLength
            ? slug
            : slug[..maxSlugLength].TrimEnd('-');
        return $"{trimmedSlug}{suffixPart}";
    }
}
