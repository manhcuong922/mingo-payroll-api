using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Enums.Payroll;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// ⚠️ <b>File tạm — các lỗi CÒN MỞ của báo cáo review 01/08/2026 chưa được sửa.</b>
///
/// <para>Mỗi test assert theo hành vi ĐÚNG nên hiện ĐỎ — đó là bằng chứng lỗi có thật. Khi một lỗi
/// được sửa, test tương ứng được chuyển sang file riêng theo chủ đề (xem
/// <see cref="PayrollCollectorOvertimeTests"/> cho A1, <see cref="DayTypeResolverRestDayTests"/>
/// cho A2) rồi commit cùng bản sửa, để mọi commit trên nhánh đều xanh. File này rỗng dần và được
/// xoá khi hết lỗi.</para>
///
/// <para>Còn lại: A4 (cột Thưởng dự án luôn bằng 0), C3 (CCCD làm mã NV),
/// D3 (trần BHYT không có tác dụng).</para>
/// </summary>
public sealed class PayrollDataCollectorBugTests
{
    // ===================== A4 (phần Thưởng dự án) — cột vĩnh viễn bằng 0 =====================
    //
    // Phần PHÍ CÔNG ĐOÀN của A4 đã sửa xong (xem PayrollUnionDuesTests): đoàn phí nay tính từ tỷ lệ
    // + trần khai trong Compliance, nhân cờ đoàn viên trên hồ sơ NV. Phần Thưởng dự án dưới đây thì
    // KHÔNG có nguồn nào để tra — hệ thống không có khái niệm "dự án có thưởng" hay bảng phân bổ
    // thưởng theo NLĐ. Hai lối ra: (a) mở cột thành Input cho kế toán nhập như Thưởng năm/Thưởng
    // khác bên cạnh, hoặc (b) dựng nguồn dữ liệu thật. Chờ PO chốt.

    [Fact(DisplayName = "A4 · Thưởng dự án phải được tra từ dữ liệu hoặc cho nhập tay, không gán cứng 0")]
    public async Task A4_ProjectBonus_MustNotBeHardcodedZero()
    {
        using var db = await SeedBaseAsync();
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers.Should().ContainKey(PayrollFieldKeys.BonusThuongDuAn);
        inputs.Numbers[PayrollFieldKeys.BonusThuongDuAn].Should().NotBe(0m,
            "cột luôn = 0 với mọi dữ liệu đầu vào ⇒ NLĐ không bao giờ nhận thưởng dự án; " +
            "cần nguồn tra thật hoặc mở cho kế toán sửa tay");
    }

    // ===================== C3 — CCCD làm mã nhân viên =====================

    [Fact(DisplayName = "C3 · NV chưa có tài khoản: cột Mã NV không được lấy số CCCD")]
    public async Task C3_EmployeeCode_MustNotFallBackToCitizenId()
    {
        using var db = await SeedBaseAsync(employeeCode: null); // NV chưa được cấp tài khoản
        db.AttendanceRecords.Add(ShiftRecord(day: 6));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Texts[PayrollFieldKeys.EmployeeCode].Should().NotBe(CitizenId,
            "bảng lương và file Excel luân chuyển giữa kế toán/ngân hàng/BHXH ⇒ không được mang số CCCD");
    }

}
