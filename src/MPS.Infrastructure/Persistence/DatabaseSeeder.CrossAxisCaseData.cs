using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Payroll;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Dữ liệu cho các case <b>LIÊN TRỤC</b> (X-series) — những chỗ mà từng trục nghiệp vụ đứng riêng thì
/// engine tính đúng, nhưng nhân hai trục vào nhau mới sai.
///
/// <para><b>Vì sao phải seed riêng.</b> Các case một-trục (loại hợp đồng, vào/nghỉ giữa tháng, vượt
/// trần, tăng ca, phụ cấp ngoại lệ…) đã có dữ liệu ở <c>DatabaseSeeder.WorkforceContractsData.cs</c>,
/// <c>PayrollCaseData.cs</c> và <c>AttendanceCaseData.cs</c>. Nhưng bộ dữ liệu đó cố ý cho mỗi nhân
/// vật ĐÚNG MỘT tình huống, nên không dòng lương nào mang hai điều kiện cùng lúc — mà đó chính là chỗ
/// các lỗi còn lại nằm. Lớp này ghép các điều kiện lại trên cùng một người, cùng một kỳ.</para>
///
/// <para><b>Trục thời gian.</b> Mọi mốc bám <c>DemoCalendar</c> (kỳ 0 = 05/2026, kỳ 1 = 06/2026,
/// kỳ 2 = 07/2026). Trần đóng BHXH/BHYT đổi 46.800.000 → 50.600.000 từ 01/07/2026, nên kỳ 05 và 06
/// dùng trần cũ, kỳ 07 dùng trần mới.</para>
///
/// <para><b>Idempotent</b> theo mã phụ lục / (nhân viên, ngày bắt đầu đơn) như các lớp seed khác.
/// ⚠️ Phụ cấp hợp đồng chỉ gắn cho hợp đồng CHƯA có dòng phụ cấp nào
/// (<c>SeedWorkforceAllowancesAsync</c>), nên các mã mới (RESP/UNIFORM/PERDIEM) chỉ xuất hiện trên
/// DB dựng lại từ đầu — DB cũ phải xoá hoặc bổ sung tay.</para>
///
/// <para>Bảng case → xem <c>docs/plans/payroll-cross-axis-gaps-2026-08.md</c>.</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    /// <summary>
    /// Username của người gánh cả hai case X1a/X1b — Giám đốc, chủ nhân mức lương DUY NHẤT của công
    /// ty vượt trần đóng BHXH. Xem ghi chú ràng buộc ở <see cref="SeedTopEarnerRaiseAppendixAsync"/>.
    /// </summary>
    private const string TopEarnerUsername = "director";

    /// <summary>Phụ lục nâng bậc Giám đốc GD-1 → GD-4, hiệu lực giữa kỳ 05 (case X1b).</summary>
    private const string TopEarnerRaiseAppendixNo = "PL-X1B-NANGBAC-GD";

    /// <summary>Bậc lương đích của phụ lục X1b — bậc duy nhất của công ty vượt cả hai mốc trần.</summary>
    private const string TopEarnerTopGradeCode = "GD-4";

    /// <summary>
    /// ⭐ X1b — <b>Trần đóng BH bị áp SAU khi đã bình quân mức lương cả kỳ.</b>
    ///
    /// <para>Giám đốc hưởng GD-1 (36.000.000đ) đến hết 15/05/2026, từ 16/05 lên GD-4 (55.000.000đ).
    /// Nền đóng BH là LƯƠNG CỘNG PHỤ CẤP LƯƠNG, mà Giám đốc có phụ cấp trách nhiệm <c>RESP</c>
    /// 10.000.000đ mang cờ "tính vào nền BHXH" ⇒ hai đoạn VẮT QUA trần 46.800.000đ của kỳ 05:
    /// nửa đầu 46tr (dưới trần), nửa sau 65tr (trên trần).</para>
    ///
    /// <para>Đúng luật thì trần chặn MỨC LƯƠNG THÁNG của TỪNG ĐOẠN, cắt xong mới bình quân theo ngày
    /// công. Engine cũ làm ngược — bình quân cả kỳ rồi mới đưa qua <c>BH_CO_TRAN</c>, tức
    /// <c>MIN(bình_quân, trần)</c> — mà <c>MIN</c> không giao hoán với phép bình quân.</para>
    ///
    /// <para><b>Số kỳ vọng</b> (công chuẩn 05/2026 = 21 ngày, trong đó 01/05 là ngày lễ được bù công;
    /// 16–17/05 rơi vào T7/CN nên đoạn mức mới bắt đầu tính công từ 18/05):
    /// <list type="bullet">
    ///   <item>Kẹp trần từng đoạn rồi bình quân: (11 × 46.000.000 + 10 × 46.800.000) / 21 =
    ///     <b>46.380.952đ</b> — số ĐÚNG.</item>
    ///   <item>Bình quân rồi mới kẹp: (11 × 36tr + 10 × 55tr)/21 + 10tr = 55.047.619 ⇒ cắt còn
    ///     <b>46.800.000đ</b> — số SAI của engine cũ.</item>
    ///   <item>Chênh nền đóng <b>419.048đ</b> ⇒ BHXH NLĐ 8% lệch <b>33.524đ</b>, DN 17,5% (gộp HT 14%
    ///     + ỐĐ-TS 3% + TNLĐ-BNN 0,5%) lệch <b>73.333đ</b> — engine cũ thu THỪA.</item>
    /// </list></para>
    ///
    /// <para>Kỳ 06 và 07 Giám đốc ở GD-4 nguyên bậc; kỳ 07 đủ công nên nền đóng = ĐÚNG BẰNG trần mới
    /// 50,6tr — nhánh "chạm trần" của case C13 giữ nguyên độ phủ, và là ĐỐI CHỨNG nội tại chứng minh
    /// engine chỉ sai khi có thêm trục thứ hai (đổi lương giữa kỳ / thiếu công).</para>
    ///
    /// <para>Dùng chung cơ chế <c>[D12]</c> với phụ lục C3: mức lương theo mốc phụ lục, kỳ trước mốc
    /// tính lại vẫn ra mức cũ.</para>
    ///
    /// <para><b>Vì sao buộc phải là Giám đốc.</b> Case X1 đòi mức lương THÁNG vượt trần đóng BHXH.
    /// Toàn thang bậc công ty chỉ có GD-4 (55tr) vượt mốc 50,6tr; bậc cao thứ nhì là KTT-3/TPHR-3 =
    /// 25tr, cách trần hơn gấp đôi. Không chức danh nào khác gánh được case này mà không phải bịa ra
    /// một vị trí không tồn tại trong công ty.</para>
    /// </summary>
    private async Task SeedTopEarnerRaiseAppendixAsync(CancellationToken ct)
    {
        if (await _context.ContractAppendices.AsNoTracking()
                .AnyAsync(a => a.AppendixNo == TopEarnerRaiseAppendixNo, ct)) return;

        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (adminId == 0) return;

        var contract = await FindActiveContractByUsernameAsync(TopEarnerUsername, ct);
        if (contract is null)
        {
            _logger.LogDebug("X1b: chưa có hợp đồng Giám đốc; bỏ qua phụ lục nâng bậc.");
            return;
        }

        var topSalary = await _context.SalaryGrades.AsNoTracking()
            .Where(g => g.Code == TopEarnerTopGradeCode)
            .Select(g => (decimal?)g.BaseSalary)
            .FirstOrDefaultAsync(ct);
        if (topSalary is not decimal newSalary)
        {
            _logger.LogWarning("X1b: thiếu bậc lương {Code}; bỏ qua phụ lục nâng bậc Giám đốc.",
                TopEarnerTopGradeCode);
            return;
        }
        if (newSalary <= contract.SalaryAmountSnapshot)
        {
            _logger.LogWarning(
                "X1b: bậc {Code} ({New:N0}) không cao hơn mức đang hưởng ({Old:N0}); case mất tác dụng.",
                TopEarnerTopGradeCode, newSalary, contract.SalaryAmountSnapshot);
            return;
        }

        var effective = DemoDay(0, 16);
        await _context.ContractAppendices.AddAsync(new ContractAppendix
        {
            ContractId = contract.Id,
            AppendixNo = TopEarnerRaiseAppendixNo,
            AppendixType = ContractEnums.AppendixType.Amendment,
            Title = "Nâng bậc lương Giám đốc điều hành",
            ChangedClausesJson = PayrollJson.Serialize(new { salaryAmount = newSalary }),
            AppendixText =
                $"Hai bên thoả thuận nâng mức lương từ {contract.SalaryAmountSnapshot:N0} VND lên "
                + $"{newSalary:N0} VND/tháng kể từ ngày {effective:dd/MM/yyyy}, theo Nghị quyết Hội đồng "
                + "thành viên về việc "
                + "kiêm nhiệm người đại diện theo pháp luật.",
            SalaryAmountSnapshot = newSalary,
            SalaryCurrencySnapshot = "VND",
            NoticeSentAt = Utc(effective.AddDays(-10)),
            EffectiveFrom = Utc(effective),
            SignedAt = Utc(effective.AddDays(-3)),
            Status = ContractEnums.AppendixStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminId,
        }, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "X1b: phụ lục nâng bậc Giám đốc {Old:N0} → {New:N0} hiệu lực {Date:dd/MM/yyyy}.",
            contract.SalaryAmountSnapshot, newSalary, effective);
    }
}
