using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using Xunit;

namespace MPS.Application.Tests.Common.Payroll;

/// <summary>
/// Chỉ tiêu [37] của tờ khai 02/QTT-TNCN là thuế của CẢ NĂM, tính trên biểu luỹ tiến năm
/// (ngưỡng bậc tháng ×12) — không phải tổng thuế đã khấu trừ 12 tháng. Hai con số này lệch
/// nhau khi thu nhập không đều giữa các tháng, và chênh lệch chính là số NLĐ được hoàn / phải nộp thêm.
/// </summary>
public class AnnualPitCalculatorTests
{
    /// <summary>Biểu luỹ tiến từng phần theo tháng (Phụ lục 01/PL-TNCN), đơn vị đồng.</summary>
    private static IReadOnlyList<PitBracketSnapshot> MonthlyBrackets() =>
    [
        new(1, 0m, 5_000_000m, 0.05m),
        new(2, 5_000_000m, 10_000_000m, 0.10m),
        new(3, 10_000_000m, 18_000_000m, 0.15m),
        new(4, 18_000_000m, 32_000_000m, 0.20m),
        new(5, 32_000_000m, 52_000_000m, 0.25m),
        new(6, 52_000_000m, 80_000_000m, 0.30m),
        new(7, 80_000_000m, null, 0.35m),
    ];

    [Fact]
    public void Compute_ThuNhapTinhThueBangKhong_ThiThueBangKhong()
    {
        AnnualPitCalculator.Compute(0m, MonthlyBrackets()).Should().Be(0m);
    }

    [Fact]
    public void Compute_ThuNhapAm_ThiThueBangKhong()
    {
        // Giảm trừ lớn hơn thu nhập ⇒ không có thuế, không được ra số âm.
        AnnualPitCalculator.Compute(-5_000_000m, MonthlyBrackets()).Should().Be(0m);
    }

    [Fact]
    public void Compute_TronBacMotNam_DungNguong60Trieu()
    {
        // Bậc 1 theo năm = 5 triệu × 12 = 60 triệu, thuế suất 5%.
        AnnualPitCalculator.Compute(60_000_000m, MonthlyBrackets())
            .Should().Be(3_000_000m);
    }

    [Fact]
    public void Compute_VuotBacMot_ThiPhanVuotTinhTheoBacHai()
    {
        // 72tr = 60tr×5% + 12tr×10% = 3.000.000 + 1.200.000
        AnnualPitCalculator.Compute(72_000_000m, MonthlyBrackets())
            .Should().Be(4_200_000m);
    }

    [Fact]
    public void Compute_BacCaoNhat_KhongCoTran()
    {
        // Bậc 7 theo năm bắt đầu từ 80tr×12 = 960tr, thuế suất 35%.
        // 960tr: 60×5% + 60×10% + 96×15% + 168×20% + 240×25% + 336×30%
        //      = 3 + 6 + 14,4 + 33,6 + 60 + 100,8 = 217,8tr
        AnnualPitCalculator.Compute(960_000_000m, MonthlyBrackets())
            .Should().Be(217_800_000m);

        // Thêm 100tr vượt bậc cuối ⇒ cộng đúng 35 triệu.
        AnnualPitCalculator.Compute(1_060_000_000m, MonthlyBrackets())
            .Should().Be(217_800_000m + 35_000_000m);
    }

    [Fact]
    public void Compute_KhacVoiTongThueThangKhiThuNhapKhongDeu()
    {
        // Cả năm nhận 120tr nhưng dồn vào 1 tháng: khấu trừ tháng đó rất cao,
        // còn quyết toán năm chỉ ở bậc thấp ⇒ NLĐ được hoàn. Đây chính là lý do
        // [37] phải tính lại chứ không cộng dồn [39].
        var annual = AnnualPitCalculator.Compute(120_000_000m, MonthlyBrackets());
        var withheldInOneMonth = AnnualPitCalculator.Compute(120_000_000m, MonthlyBrackets(), months: 1);

        annual.Should().BeLessThan(withheldInOneMonth);
    }

    [Fact]
    public void Compute_ThieuBieuThue_ThiNem()
    {
        var act = () => AnnualPitCalculator.Compute(60_000_000m, []);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Compute_BieuThueKhongTheoThuTu_VanTinhDung()
    {
        var shuffled = MonthlyBrackets().OrderByDescending(b => b.Order).ToList();

        AnnualPitCalculator.Compute(72_000_000m, shuffled).Should().Be(4_200_000m);
    }
}
