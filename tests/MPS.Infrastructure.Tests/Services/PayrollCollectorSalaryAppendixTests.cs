using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Domain.Entities;
using static MPS.Infrastructure.Tests.Services.PayrollCollectorFixture;
using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Infrastructure.Tests.Services;

/// <summary>
/// A5 — <b>Quyết định đã chốt (PO, 01/08/2026): GIỮ NGUYÊN.</b> Phụ lục đổi lương chỉ áp từ kỳ
/// lương KẾ TIẾP, không chia theo ngày hiệu lực trong kỳ đang chạy.
///
/// <para>Lý do thiết kế (ghi chú <c>[D12]</c> tại <c>PayrollDataCollector</c>): mốc lương của kỳ
/// chốt theo phụ lục có hiệu lực KHÔNG MUỘN HƠN đầu kỳ, nhờ vậy tính lại một kỳ CŨ luôn ra đúng
/// mức lương của thời điểm đó.</para>
///
/// <para>Đây là <b>test ghim quyết định</b>, không phải test lỗi: nếu ai đó sau này đổi sang chia
/// theo ngày hiệu lực, test đỏ và buộc phải quay lại đọc quyết định này trước.
/// ⚠️ Ràng buộc kèm theo: quy chế lương phải ghi rõ điều khoản này cho NLĐ.</para>
/// </summary>
public sealed class PayrollCollectorSalaryAppendixTests
{
    private const decimal RaisedSalary = 12_000_000m;

    private static ContractAppendix SalaryAppendix(DateTime effectiveFrom) => new()
    {
        Id = 1,
        ContractId = ContractId,
        AppendixNo = "PL-001",
        Status = ContractEnums.AppendixStatus.Active,
        EffectiveFrom = effectiveFrom,
        SalaryAmountSnapshot = RaisedSalary,
    };

    [Fact(DisplayName = "A5 · [Quyết định] Phụ lục tăng lương giữa tháng KHÔNG áp cho kỳ đang chạy")]
    public async Task MidMonthAppendix_IsDeliberatelyDeferredToNextPeriod()
    {
        using var db = await SeedBaseAsync();
        db.ContractAppendices.Add(SalaryAppendix(new DateTime(Year, Month, 15)));
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(Salary,
            "[D12] — kỳ 07 giữ nguyên mức lương cũ; mức mới bắt đầu từ kỳ 08");
    }

    [Fact(DisplayName = "A5 · [Quyết định] Phụ lục hiệu lực TỪ ĐẦU KỲ thì áp ngay kỳ đó")]
    public async Task AppendixEffectiveFromPeriodStart_AppliesImmediately()
    {
        using var db = await SeedBaseAsync();
        db.ContractAppendices.Add(SalaryAppendix(new DateTime(Year, Month, 1)));
        db.AttendanceRecords.Add(ShiftRecord(day: 20));
        await db.SaveChangesAsync();

        var inputs = await CollectAsync(db);

        inputs.Numbers[PayrollFieldKeys.ContractMucLuong].Should().Be(RaisedSalary,
            "ranh giới của quyết định: hiệu lực đúng đầu kỳ thì kỳ đó ăn mức mới — " +
            "ghim để phân biệt 'cố ý hoãn' với 'phụ lục bị bỏ quên hoàn toàn'");
    }
}
