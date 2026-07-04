using System.Globalization;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeSlotTemplate.Common;

/// <summary>
/// Code do BE tự sinh theo khung giờ (VD 18:00–20:00 → <c>OT-1800-2000</c>).
/// Nếu trùng, thêm suffix <c>-2</c>, <c>-3</c>, ... cho tới khi unique. Code là identifier
/// hiển thị trong UI/audit khi entity không có Name.
/// </summary>
internal static class OvertimeSlotCodeGenerator
{
    private const string Prefix = "OT-";

    public static async Task<string> GenerateUniqueAsync(
        TimeOnly startTime,
        TimeOnly endTime,
        IOvertimeSlotTemplateRepository repository,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        var baseCode = BuildCandidate(startTime, endTime, suffix: null);
        if (!await repository.ExistsByCodeAsync(baseCode, excludeId, cancellationToken))
        {
            return baseCode;
        }

        for (var sequence = 2; sequence <= 999; sequence++)
        {
            var candidate = BuildCandidate(startTime, endTime, sequence.ToString(CultureInfo.InvariantCulture));
            if (!await repository.ExistsByCodeAsync(candidate, excludeId, cancellationToken))
            {
                return candidate;
            }
        }

        throw new ConflictException($"Không thể tạo mã khung giờ OT duy nhất cho {startTime:HH:mm}–{endTime:HH:mm}.");
    }

    private static string BuildCandidate(TimeOnly start, TimeOnly end, string? suffix)
    {
        var suffixPart = string.IsNullOrEmpty(suffix) ? string.Empty : $"-{suffix}";
        return $"{Prefix}{start:HHmm}-{end:HHmm}{suffixPart}";
    }
}
