using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// BS-5 — quy tắc gom khoản truy lĩnh/truy thu chưa áp vào một kỳ lương mới.
///
/// Truy lĩnh (+) luôn áp trọn. Truy thu (−) bị giới hạn bởi dư địa THỰC NHẬN của kỳ:
/// chốt PO 2026-07-21 — <b>thực nhận không được âm</b>; phần chưa trừ hết ở lại dạng chưa áp để kỳ
/// kế tiếp gom tiếp. Khoản cũ được trừ trước (đã sắp theo CreatedAt ở repository).
///
/// Khoản bị áp MỘT PHẦN được tách làm 2 bản ghi: phần đã áp (gắn kỳ mới) + phần dư (vẫn chưa áp),
/// để lịch sử truy vết được số tiền thực trừ ở từng kỳ.
/// </summary>
public static class PayrollAdjustmentApplier
{
    /// <summary>Kết quả gom cho 1 nhân viên.</summary>
    /// <param name="TruyLinh">Tổng phần dương — luôn ≥ 0.</param>
    /// <param name="TruyThu">Tổng phần âm ĐÃ CLAMP theo dư địa — luôn ≤ 0.</param>
    /// <param name="Applied">Khoản (hoặc phần khoản) được áp vào kỳ này.</param>
    /// <param name="Carried">Bản ghi phần dư mới cần thêm (khoản bị áp một phần).</param>
    public sealed record Plan(
        decimal TruyLinh,
        decimal TruyThu,
        IReadOnlyList<PayrollAdjustment> Applied,
        IReadOnlyList<PayrollAdjustment> Carried);

    /// <summary>Tổng phần dương của tập khoản chưa áp (áp trọn, không phụ thuộc dư địa).</summary>
    public static decimal SumPositives(IEnumerable<PayrollAdjustment> pending) =>
        pending.Where(a => a.Amount > 0).Sum(a => a.Amount);

    /// <summary>
    /// Phân bổ các khoản âm trong <paramref name="pending"/> vào dư địa
    /// <paramref name="availableNet"/> (THỰC NHẬN đã gồm truy lĩnh, chưa trừ truy thu).
    /// </summary>
    public static Plan Build(
        IReadOnlyList<PayrollAdjustment> pending,
        decimal availableNet,
        DateTime nowUtc)
    {
        var truyLinh = SumPositives(pending);
        var applied = new List<PayrollAdjustment>();
        var carried = new List<PayrollAdjustment>();

        // Truy lĩnh áp trọn ngay.
        foreach (var positive in pending.Where(a => a.Amount > 0))
        {
            positive.AppliedAt = nowUtc;
            applied.Add(positive);
        }

        var remainingCapacity = Math.Max(0m, availableNet);
        var deducted = 0m;

        foreach (var negative in pending.Where(a => a.Amount < 0))
        {
            if (remainingCapacity <= 0m) break;

            var wanted = -negative.Amount;                       // dương
            var take = Math.Min(wanted, remainingCapacity);

            if (take >= wanted)
            {
                // Trừ trọn khoản này.
                negative.AppliedAt = nowUtc;
                applied.Add(negative);
            }
            else
            {
                // Trừ một phần: bản ghi gốc giữ phần dư (chưa áp), tách bản ghi mới cho phần đã trừ.
                negative.Amount = -(wanted - take);
                carried.Add(new PayrollAdjustment
                {
                    EmployeeInformationId = negative.EmployeeInformationId,
                    SourcePeriodId = negative.SourcePeriodId,
                    Amount = -take,
                    Reason = $"{negative.Reason} (trừ một phần)",
                    CreatedAt = negative.CreatedAt,
                    CreatedByUserId = negative.CreatedByUserId,
                    AppliedAt = nowUtc,
                });
            }

            deducted += take;
            remainingCapacity -= take;
        }

        return new Plan(truyLinh, -deducted, applied, carried);
    }
}
