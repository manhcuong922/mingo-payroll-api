using FluentValidation;
using MPS.Application.Features.Payroll.Commands;

namespace MPS.Application.Features.Payroll.Validators;

/// <summary>
/// Lỗi #43 — <c>POST /payroll/periods</c> trước đây KHÔNG có validator nào.
///
/// <para><b>Nghiệp vụ.</b> Số ngày công chuẩn là <b>mẫu số của toàn bộ phép chia lương</b>
/// (<c>IncomeProrationFunction</c>): thu nhập theo ngày công = mức lương ÷ công chuẩn × công thực
/// tế. Vì vậy nó bắt buộc phải là số DƯƠNG.</para>
///
/// <para><b>Hậu quả khi thiếu guard</b> (đo thật trên kỳ 34 dòng lương):
/// <list type="bullet">
///   <item>công chuẩn <c>0</c> → kỳ vẫn tạo thành công <c>201</c>, thu nhập của mọi người về 0,
///         thực nhận <c>−945.000</c>, <b>34/34 dòng lương âm</b>;</item>
///   <item>công chuẩn <c>−5</c> → thu nhập <c>−82.800.000</c>, thực nhận <c>−83.745.000</c>.</item>
/// </list>
/// Chỗ nguy hiểm nhất: hàm chia có né chia-0 (<c>if (j != 0)</c>) nên <b>không nổ exception</b> —
/// hệ thống báo "tạo kỳ lương thành công", không một cảnh báo nào, và cả công ty nhận lương 0
/// hoặc âm. Một lỗi gõ phím của kế toán là đủ.</para>
///
/// <para><b>Không đặt trần trên.</b> `CAC-CASE-XAY-RA.md` §11.2 chốt: nhập số rất lớn (ví dụ 999)
/// thì <b>vẫn cho tạo</b> — công chuẩn ghi đúng, thu nhập ra số hữu hạn không âm. Đó là quyết định
/// có chủ đích (kế toán tự chịu trách nhiệm con số mình nhập), nên chỉ chặn ≤ 0.
/// Ghim bằng <c>PAY-SWD01</c> / <c>PAY-SWD02</c> (chặn) và <c>PAY-SWD03</c> (999 vẫn tạo được).</para>
///
/// <para>Tháng hợp lệ vẫn do handler kiểm (<c>DomainValidationException</c>) để giữ nguyên mã lỗi
/// và thông điệp mà <c>PAY-04</c> đang bám theo.</para>
/// </summary>
public sealed class CreatePayrollPeriodCommandValidator : AbstractValidator<CreatePayrollPeriodCommand>
{
    /// <summary>Trần dưới: phải dương thực sự, không nhận 0 và không nhận số âm.</summary>
    private const decimal MinStandardWorkingDays = 0.01m;

    public CreatePayrollPeriodCommandValidator()
    {
        // Chỉ kiểm khi người dùng CÓ gửi. Bỏ trống là hợp lệ — handler tự suy ra số ngày làm việc
        // của tháng (`PayrollWorkingDays.CountWeekdays`).
        RuleFor(x => x.StandardWorkingDays)
            .GreaterThanOrEqualTo(MinStandardWorkingDays)
            .When(x => x.StandardWorkingDays.HasValue)
            .WithMessage(
                "Số ngày công chuẩn phải lớn hơn 0 — đây là mẫu số của mọi phép chia lương, "
                + "nhận giá trị 0 hoặc âm sẽ làm toàn bộ bảng lương ra số 0 hoặc số âm.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100)
            .WithMessage("Năm của kỳ lương không hợp lệ.");
    }
}
