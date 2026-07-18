using FluentValidation;
using MPS.Application.Features.Attendance.Attendance.Queries;

namespace MPS.Application.Features.Attendance.Attendance.Validators;

/// <summary>
/// Range tối đa 92 ngày (~3 tháng) — chống timeout khi HR chọn cả năm với 500+ NV.
/// </summary>
public class GetAttendanceSummaryQueryValidator : AbstractValidator<GetAttendanceSummaryQuery>
{
    private const int MaxRangeDays = 92;

    public GetAttendanceSummaryQueryValidator()
    {
        RuleFor(x => x.FromDate)
            .LessThanOrEqualTo(x => x.ToDate)
            .WithMessage("FromDate phải nhỏ hơn hoặc bằng ToDate.");

        RuleFor(x => x)
            .Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber + 1 <= MaxRangeDays)
            .WithMessage($"Khoảng thời gian tối đa {MaxRangeDays} ngày.");

        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        // FE có thể chọn size lớn để sort/filter toàn bộ NV client-side (bảng chấm công tháng).
        // Range đã cap 92 ngày nên load 1000 NV × 92 ngày vẫn ok về perf.
        RuleFor(x => x.Size).InclusiveBetween(1, 1000);
    }
}
