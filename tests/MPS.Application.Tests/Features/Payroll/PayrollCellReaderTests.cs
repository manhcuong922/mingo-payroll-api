using FluentAssertions;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;
using Xunit;

namespace MPS.Application.Tests.Features.Payroll;

public class PayrollCellReaderTests
{
    private static PayrollRow RowWith(params PayrollCellValue[] cells)
        => new() { Cells = cells.ToList() };

    private static PayrollCellValue Cell(
        string code, decimal? computed, decimal? over = null, bool isOverridden = false)
        => new()
        {
            ColumnCode = code,
            ComputedValue = computed,
            OverrideValue = over,
            IsOverridden = isOverridden,
        };

    [Fact]
    public void Value_KhongSuaTay_ThiLayGiaTriEngineTinh()
    {
        var row = RowWith(Cell("THUCNHAN", 7_000_000m));

        PayrollCellReader.Value(row, "THUCNHAN").Should().Be(7_000_000m);
    }

    [Fact]
    public void Value_KeToanSuaTay_ThiLayGiaTriSuaTay()
    {
        // Đây là quy tắc quan trọng nhất: chứng từ chi trả phải bám số kế toán đã chốt.
        var row = RowWith(Cell("THUCNHAN", 7_000_000m, over: 6_500_000m, isOverridden: true));

        PayrollCellReader.Value(row, "THUCNHAN").Should().Be(6_500_000m);
    }

    [Fact]
    public void Value_KhongPhanBietHoaThuong()
    {
        var row = RowWith(Cell("THUCNHAN", 1_000m));

        PayrollCellReader.Value(row, "thucnhan").Should().Be(1_000m);
    }

    [Fact]
    public void Value_ThieuCot_ThiTraNull()
    {
        var row = RowWith(Cell("THUCNHAN", 1_000m));

        PayrollCellReader.Value(row, "KHONG_TON_TAI").Should().BeNull();
    }

    [Fact]
    public void ValueOrZero_ThieuCotHoacORong_ThiTraKhong()
    {
        var row = RowWith(Cell("TANGCA", null));

        PayrollCellReader.ValueOrZero(row, "TANGCA").Should().Be(0m);
        PayrollCellReader.ValueOrZero(row, "KHONG_TON_TAI").Should().Be(0m);
    }

    [Fact]
    public void ValueOrZero_SuaTayThanhRong_ThiTraKhongChuKhongLayGiaTriEngine()
    {
        // Kế toán chủ động xoá trắng ô ⇒ coi như không phát sinh, không được rơi về số engine.
        var row = RowWith(Cell("THUONGNAM", 5_000_000m, over: null, isOverridden: true));

        PayrollCellReader.ValueOrZero(row, "THUONGNAM").Should().Be(0m);
    }

    [Fact]
    public void SumOrZero_CongDonNhieuKy()
    {
        var rows = new[]
        {
            RowWith(Cell("THUETNCN", 300_000m)),
            RowWith(Cell("THUETNCN", 450_000m, over: 400_000m, isOverridden: true)),
            RowWith(Cell("TANGCA", 1_000m)),   // kỳ không có cột thuế
        };

        PayrollCellReader.SumOrZero(rows, "THUETNCN").Should().Be(700_000m);
    }
}
