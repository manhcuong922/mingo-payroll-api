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
/// <para>Còn lại: C3 (CCCD làm mã NV). A4 (cột Thưởng dự án luôn bằng 0) đã sửa — PO chốt
/// 19/08/2026 mở cột thành Input cho kế toán nhập tay, xem <c>ProjectBonusColumnTests</c>;
/// D3 (trần BHYT) đã sửa.</para>
/// </summary>
public sealed class PayrollDataCollectorBugTests
{
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
