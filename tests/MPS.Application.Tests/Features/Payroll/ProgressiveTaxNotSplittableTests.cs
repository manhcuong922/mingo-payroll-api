using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll.Formula;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// ⭐ [Mục 3] Bằng chứng số học cho quyết định "KHÔNG chia thuế TNCN theo ngày".
///
/// <para>Đây không phải test một tính năng — nó ghi lại LÝ DO. Nếu sau này có người đề xuất chẻ kỳ
/// lương ra tính thuế riêng từng nửa tháng, bộ test này chỉ thẳng hậu quả: thuế lũy tiến là hàm LỒI
/// nên tổng thuế của hai nửa LUÔN ≤ thuế của cả tháng, tức doanh nghiệp khấu trừ THIẾU và phần chênh
/// lộ ra ở quyết toán năm.</para>
///
/// <para>Biểu thuế dùng ở đây theo Điều 22 Luật Thuế TNCN (bậc tháng, triệu đồng).</para>
/// </summary>
public sealed class ProgressiveTaxNotSplittableTests
{
    private static readonly IReadOnlyList<PitBracketSnapshot> Brackets = new[]
    {
        new PitBracketSnapshot(1, 0m, 5_000_000m, 0.05m),
        new PitBracketSnapshot(2, 5_000_000m, 10_000_000m, 0.10m),
        new PitBracketSnapshot(3, 10_000_000m, 18_000_000m, 0.15m),
        new PitBracketSnapshot(4, 18_000_000m, 32_000_000m, 0.20m),
        new PitBracketSnapshot(5, 32_000_000m, 52_000_000m, 0.25m),
        new PitBracketSnapshot(6, 52_000_000m, 80_000_000m, 0.30m),
        new PitBracketSnapshot(7, 80_000_000m, null, 0.35m),
    };

    private static decimal Tax(decimal taxableIncome)
    {
        var ctx = new PayrollFunctionContext { PitBrackets = Brackets };
        return new ProgressiveTaxFunction().Invoke(new[] { taxableIncome }, ctx).Value;
    }

    [Theory]
    [InlineData(20_000_000)]   // vắt bậc 3 → bậc 4
    [InlineData(40_000_000)]   // vắt bậc 4 → bậc 5
    [InlineData(100_000_000)]  // chạm bậc cao nhất
    public void ChiaDoiKyRoiTinhThueRieng_LuonThuTHIEU_SoVoiTinhCaThang(decimal monthlyTaxable)
    {
        // Chẻ đôi thu nhập tính thuế rồi áp CÙNG biểu thuế tháng cho mỗi nửa — đúng cách mà đề xuất
        // "chia thuế theo ngày" sẽ dẫn tới.
        var half = monthlyTaxable / 2m;
        var splitTotal = Tax(half) + Tax(half);

        var wholeMonth = Tax(monthlyTaxable);

        splitTotal.Should().BeLessThan(wholeMonth,
            "thuế lũy tiến là hàm lồi ⇒ chẻ kỳ luôn khiến tổng thuế khấu trừ thấp hơn số thực phải nộp");
    }

    [Fact]
    public void MucThieuHutKhongHeNhoNhat_VaiTrieuDongMotNguoiMotThang()
    {
        // Cụ thể hoá để người đọc thấy quy mô, không chỉ thấy dấu bất đẳng thức.
        // TNTT 40tr: cả tháng = 5tr×5% + 5tr×10% + 8tr×15% + 14tr×20% + 8tr×25% = 6,75tr
        //            chẻ đôi  = 2 × (5tr×5% + 5tr×10% + 8tr×15% + 2tr×20%) = 2 × 2,35tr = 4,7tr
        var wholeMonth = Tax(40_000_000m);
        var splitTotal = 2m * Tax(20_000_000m);

        wholeMonth.Should().Be(6_750_000m);
        splitTotal.Should().Be(4_700_000m);
        (wholeMonth - splitTotal).Should().Be(2_050_000m);
    }

    [Fact]
    public void ChiKhiThuNhapNamTRON_TrongMotBac_ThiChiaMoiKhongLech()
    {
        // Ranh giới của lập luận: hàm lồi nhưng TUYẾN TÍNH TỪNG KHÚC, nên trong cùng một bậc thì
        // chia hay không cũng như nhau. Ghi lại để không ai kết luận quá tay rằng "chia luôn sai".
        var wholeMonth = Tax(4_000_000m);          // trọn trong bậc 1
        var splitTotal = 2m * Tax(2_000_000m);

        splitTotal.Should().Be(wholeMonth);
    }

    [Fact]
    public void GiamTruGiaCanh_ChiaDoiCungKhongCoCanCuPhapLy()
    {
        // Giảm trừ là mức THEO THÁNG. Nếu chẻ kỳ, mỗi nửa lại được trừ một suất ⇒ tổng giảm trừ gấp
        // đôi và thuế gần như biến mất. Không văn bản nào cho phép điều đó.
        const decimal grossTaxable = 40_000_000m;
        const decimal personalDeduction = 11_000_000m;

        var correct = Tax(grossTaxable - personalDeduction);
        var ifSplitTwice = 2m * Tax(grossTaxable / 2m - personalDeduction);

        ifSplitTwice.Should().BeLessThan(correct);
        // Mức méo lớn tới mức không thể coi là sai số làm tròn.
        (correct - ifSplitTwice).Should().BeGreaterThan(1_000_000m);
    }
}
