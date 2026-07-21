using FluentAssertions;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// BS-5 — quy tắc gom khoản truy lĩnh/truy thu vào kỳ mới: truy lĩnh áp trọn; truy thu bị chặn bởi
/// dư địa THỰC NHẬN (không để âm), phần dư ở lại chờ kỳ sau.
/// </summary>
public sealed class PayrollAdjustmentApplierTests
{
    private static readonly DateTime Now = new(2026, 7, 21, 3, 0, 0, DateTimeKind.Utc);

    private static PayrollAdjustment Pending(decimal amount, int daysAgo = 0) => new()
    {
        Id = Math.Abs((int)amount) + daysAgo,
        EmployeeInformationId = 42,
        SourcePeriodId = 5,
        Amount = amount,
        Reason = "Duyệt OT bù",
        CreatedAt = Now.AddDays(-daysAgo),
        CreatedByUserId = 2,
    };

    [Fact]
    public void TruyLinh_ApDuTron_KhongPhuThuocDuDia()
    {
        var pending = new[] { Pending(1_500_000m), Pending(500_000m) };

        var plan = PayrollAdjustmentApplier.Build(pending, availableNet: 0m, Now);

        plan.TruyLinh.Should().Be(2_000_000m);
        plan.TruyThu.Should().Be(0m);
        plan.Applied.Should().HaveCount(2);
        plan.Applied.Should().OnlyContain(a => a.AppliedAt == Now);
        plan.Carried.Should().BeEmpty();
    }

    [Fact]
    public void TruyThu_TruDuKhiDuDiaDuLon()
    {
        var pending = new[] { Pending(-800_000m) };

        var plan = PayrollAdjustmentApplier.Build(pending, availableNet: 10_000_000m, Now);

        plan.TruyThu.Should().Be(-800_000m);
        plan.Applied.Should().ContainSingle().Which.AppliedAt.Should().Be(Now);
        plan.Carried.Should().BeEmpty();
    }

    [Fact]
    public void TruyThu_ChanVe0_PhanDuGiuLaiChoKySau()
    {
        var original = Pending(-5_000_000m);

        var plan = PayrollAdjustmentApplier.Build(new[] { original }, availableNet: 2_000_000m, Now);

        // Chỉ trừ được 2 triệu; thực nhận về 0 chứ không âm.
        plan.TruyThu.Should().Be(-2_000_000m);

        // Bản gốc giữ phần dư 3 triệu và VẪN chưa áp (AppliedAt null) để kỳ kế tiếp gom tiếp.
        original.Amount.Should().Be(-3_000_000m);
        original.AppliedAt.Should().BeNull();
        plan.Applied.Should().BeEmpty();

        // Phần đã trừ được tách thành bản ghi riêng gắn kỳ này.
        plan.Carried.Should().ContainSingle();
        plan.Carried[0].Amount.Should().Be(-2_000_000m);
        plan.Carried[0].AppliedAt.Should().Be(Now);
        plan.Carried[0].SourcePeriodId.Should().Be(original.SourcePeriodId);
    }

    [Fact]
    public void TruyThu_KhiThucNhanAm_KhongTruDongNao()
    {
        var original = Pending(-1_000_000m);

        var plan = PayrollAdjustmentApplier.Build(new[] { original }, availableNet: -50_000m, Now);

        plan.TruyThu.Should().Be(0m);
        original.Amount.Should().Be(-1_000_000m);
        original.AppliedAt.Should().BeNull();
        plan.Carried.Should().BeEmpty();
    }

    [Fact]
    public void TruyThu_KhoanCuTruTruoc_KhoanMoiConLai()
    {
        var older = Pending(-3_000_000m, daysAgo: 10);
        var newer = Pending(-2_000_000m, daysAgo: 1);

        // Dư địa 4 triệu: khoản cũ trừ trọn 3tr, khoản mới trừ được 1tr, dư 1tr.
        var plan = PayrollAdjustmentApplier.Build(new[] { older, newer }, availableNet: 4_000_000m, Now);

        plan.TruyThu.Should().Be(-4_000_000m);
        plan.Applied.Should().ContainSingle().Which.Should().BeSameAs(older);
        newer.Amount.Should().Be(-1_000_000m);
        plan.Carried.Should().ContainSingle().Which.Amount.Should().Be(-1_000_000m);
    }

    [Fact]
    public void TruyLinh_VaTruyThu_LanLuot_TruyLinhTangDuDia()
    {
        var pending = new[] { Pending(1_000_000m), Pending(-4_000_000m) };

        // availableNet ĐÃ gồm truy lĩnh (handler tính THỰC NHẬN lượt 1 với truy lĩnh rồi mới clamp).
        var plan = PayrollAdjustmentApplier.Build(pending, availableNet: 3_000_000m, Now);

        plan.TruyLinh.Should().Be(1_000_000m);
        plan.TruyThu.Should().Be(-3_000_000m);
        plan.Carried.Should().ContainSingle().Which.Amount.Should().Be(-3_000_000m);
    }
}
