using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using Xunit;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Chỉ tiêu tờ khai quyết toán thuế TNCN 02/QTT-TNCN.
///
/// <para>Đây là số NLĐ mang đi quyết toán với cơ quan thuế: khai thừa thì họ nộp oan,
/// khai thiếu thì họ bị truy thu. Nên kiểm cả giá trị lẫn những ô PHẢI để trống.</para>
/// </summary>
public class PitFinalizationCalculatorTests
{
    private static IReadOnlyList<PitBracketSnapshot> Brackets() =>
    [
        new(1, 0m, 5_000_000m, 0.05m),
        new(2, 5_000_000m, 10_000_000m, 0.10m),
        new(3, 10_000_000m, 18_000_000m, 0.15m),
        new(4, 18_000_000m, 32_000_000m, 0.20m),
        new(5, 32_000_000m, 52_000_000m, 0.25m),
        new(6, 52_000_000m, 80_000_000m, 0.30m),
        new(7, 80_000_000m, null, 0.35m),
    ];

    private static PayrollRow Month(params (string Code, decimal Value)[] cells)
        => new()
        {
            Cells = cells
                .Select(c => new PayrollCellValue { ColumnCode = c.Code, ComputedValue = c.Value })
                .ToList(),
        };

    private static PitFinalizationFigures Compute(
        IEnumerable<PayrollRow> rows, int? dependents = 0)
        => PitFinalizationCalculator.Compute(rows.ToList(), Brackets(), dependents);

    /// <summary>Một tháng lương "chuẩn" dùng lại nhiều nơi.</summary>
    private static PayrollRow StandardMonth(decimal tnct = 20_000_000m, decimal tax = 500_000m)
        => Month(
            (PayrollColumnCodes.Tnct, tnct),
            (PayrollColumnCodes.GtBanThan, 11_000_000m),
            (PayrollColumnCodes.CongBhNld, 1_000_000m),
            (PayrollColumnCodes.ThueTncn, tax));

    // ===== Quan hệ với công thức của engine =====

    [Fact]
    public void TapCotMienThue_DungBangNhungCotEngineTruKhoiTnct()
    {
        // Khẳng định lại công thức của DefaultPayrollFormulaSet:
        //   TNCT = TONGTHUNHAP − DIENTHOAI − CL_GLT − ANTRUA − PHUTROI_NGAYNGHI − PHUCAP_DEM
        // Luật đổi mà quên cập nhật PitFinalizationColumns thì test này đỏ.
        var row = Month(
            (PayrollColumnCodes.TongThuNhap, 30_000_000m),
            (PayrollColumnCodes.DienThoai, 500_000m),
            (PayrollColumnCodes.ChenhLechLtg, 2_000_000m),
            (PayrollColumnCodes.ComMienThue, 730_000m),
            (PayrollColumnCodes.PhuTroiNgayNghi, 400_000m),
            (PayrollColumnCodes.PhuCapDem, 370_000m),
            (PayrollColumnCodes.Tnct, 26_000_000m));

        var exemptTotal = PitFinalizationColumns.TaxExempt
            .Sum(code => row.Cells.First(c => c.ColumnCode == code).ComputedValue!.Value);

        var tnct = row.Cells.First(c => c.ColumnCode == PayrollColumnCodes.Tnct).ComputedValue!.Value;
        var gross = row.Cells.First(c => c.ColumnCode == PayrollColumnCodes.TongThuNhap).ComputedValue!.Value;

        (tnct + exemptTotal).Should().Be(gross);
    }

    [Fact]
    public void ChiTieu26_ChiGomPhuTroiLamThemGio_KhongGomKhoanKhoanChi()
    {
        // Điện thoại + cơm giữa ca là khoán chi theo định mức, không phải "thu nhập được miễn thuế".
        PitFinalizationColumns.ExemptIncomeToDeclare.Should().BeEquivalentTo(
        [
            PayrollColumnCodes.ChenhLechLtg,
            PayrollColumnCodes.PhuTroiNgayNghi,
            PayrollColumnCodes.PhuCapDem,
        ]);

        PitFinalizationColumns.ExemptIncomeToDeclare.Should().NotContain(PayrollColumnCodes.DienThoai);
        PitFinalizationColumns.ExemptIncomeToDeclare.Should().NotContain(PayrollColumnCodes.ComMienThue);
    }

    // ===== Cộng dồn cả năm =====

    [Fact]
    public void CongDonThuNhapVaThueCuaMoiKyTrongNam()
    {
        var figures = Compute(Enumerable.Repeat(0, 12).Select(_ => StandardMonth()));

        figures[20].Should().Be(240_000_000m);      // 20tr × 12
        figures[19].Should().Be(figures[20]);
        figures[29].Should().Be(132_000_000m);      // 11tr × 12
        figures[32].Should().Be(12_000_000m);       // 1tr × 12
        figures[39].Should().Be(6_000_000m);        // 500k × 12
        figures[38].Should().Be(figures[39]);
    }

    [Fact]
    public void TongGiamTruBangTongCacKhoanThanhPhan()
    {
        var figures = Compute(Enumerable.Repeat(0, 12).Select(_ => StandardMonth()));

        figures[28].Should().Be(figures[29] + figures[30] + figures[32]);
    }

    [Fact]
    public void ThuNhapTinhThue_KhongTruLanHaiKhoanDaMienThue()
    {
        // [19] đã là thu nhập CHỊU thuế; trừ [23] lần nữa là khai thiếu.
        var rows = Enumerable.Repeat(0, 12).Select(_ => Month(
            (PayrollColumnCodes.Tnct, 20_000_000m),
            (PayrollColumnCodes.ChenhLechLtg, 3_000_000m),
            (PayrollColumnCodes.GtBanThan, 11_000_000m),
            (PayrollColumnCodes.CongBhNld, 1_000_000m)));

        var figures = Compute(rows);

        figures[26].Should().Be(36_000_000m, "phụ trội tăng ca vẫn được khai để NLĐ thấy");
        figures[36].Should().Be(240_000_000m - 144_000_000m, "= [19] − [28], không trừ [26]");
    }

    [Fact]
    public void ThuNhapTinhThueKhongAmKhiGiamTruLonHonThuNhap()
    {
        var figures = Compute([Month(
            (PayrollColumnCodes.Tnct, 5_000_000m),
            (PayrollColumnCodes.GtBanThan, 11_000_000m))]);

        figures[36].Should().Be(0m);
        figures[37].Should().Be(0m);
    }

    // ===== Điểm cốt lõi: [37] tính lại theo biểu năm =====

    [Fact]
    public void ChiTieu37_TinhLaiTheoBieuNam_ChuKhongCongDonThueDaKhauTru()
    {
        // Thu nhập dồn vào 1 tháng: khấu trừ tháng đó rất cao, quyết toán năm thấp hơn nhiều.
        var rows = new List<PayrollRow>
        {
            Month(
                (PayrollColumnCodes.Tnct, 240_000_000m),
                (PayrollColumnCodes.GtBanThan, 11_000_000m),
                (PayrollColumnCodes.ThueTncn, 60_000_000m)),
        };

        var figures = Compute(rows);

        figures[39].Should().Be(60_000_000m, "đây là số đã khấu trừ");
        figures[37].Should().NotBe(figures[39], "[37] phải tính lại, không được lấy [39]");
        figures[37].Should().Be(
            AnnualPitCalculator.Compute(figures[36]!.Value, Brackets()));
    }

    [Fact]
    public void NopThua_ThiRaChiTieu53_VaKhongConPhaiNop()
    {
        var rows = new List<PayrollRow>
        {
            Month(
                (PayrollColumnCodes.Tnct, 240_000_000m),
                (PayrollColumnCodes.GtBanThan, 132_000_000m),
                (PayrollColumnCodes.ThueTncn, 60_000_000m)),
        };

        var figures = Compute(rows);

        figures[53].Should().BeGreaterThan(0m);
        figures[51].Should().Be(0m);
        figures[53].Should().Be(figures[38] - figures[37]);
    }

    [Fact]
    public void ConPhaiNop_ThiRaChiTieu51_VaKhongCoNopThua()
    {
        var rows = Enumerable.Repeat(0, 12).Select(_ => Month(
            (PayrollColumnCodes.Tnct, 30_000_000m),
            (PayrollColumnCodes.GtBanThan, 11_000_000m),
            (PayrollColumnCodes.ThueTncn, 0m)));

        var figures = Compute(rows);

        figures[51].Should().BeGreaterThan(0m);
        figures[53].Should().Be(0m);
    }

    // ===== Miễn thuế khoản nhỏ =====

    [Theory]
    [InlineData(0)]
    [InlineData(50_001)]
    public void ChiTieu52_BoTrongKhiNgoaiNguong(decimal stillPayable)
    {
        var figures = ComputeWithStillPayable(stillPayable);

        figures[52].Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50_000)]
    public void ChiTieu52_BangSoPhaiNopKhiTuNamMuoiNghinTroXuong(decimal stillPayable)
    {
        var figures = ComputeWithStillPayable(stillPayable);

        figures[52].Should().Be(stillPayable);
    }

    private static PitFinalizationFigures ComputeWithStillPayable(decimal stillPayable)
    {
        // Dựng ngược: không thuế đã khấu trừ, chỉnh thu nhập cho ra đúng số còn phải nộp.
        // Bậc 1 theo năm = 60tr × 5% ⇒ thu nhập tính thuế = stillPayable / 0.05
        var taxable = stillPayable / 0.05m;

        return Compute([Month(
            (PayrollColumnCodes.Tnct, taxable),
            (PayrollColumnCodes.ThueTncn, 0m))]);
    }

    // ===== Những ô PHẢI để trống =====

    [Theory]
    [InlineData(21)] // miễn giảm theo Hiệp định
    [InlineData(22)] // thu nhập phát sinh ngoài Việt Nam
    [InlineData(24)] // nhiệm vụ khoa học công nghệ
    [InlineData(25)] // chuyên gia khởi nghiệp
    [InlineData(31)] // từ thiện, nhân đạo
    [InlineData(33)] // hưu trí bổ sung, nhân thọ
    [InlineData(34)] // khám chữa bệnh
    [InlineData(35)] // giáo dục - đào tạo
    [InlineData(40)] // tự nộp không qua tổ chức trả thu nhập
    [InlineData(41)] // đã nộp ở nước ngoài
    [InlineData(54)] // đề nghị hoàn trả
    [InlineData(55)]
    [InlineData(56)]
    [InlineData(57)] // bù trừ kỳ sau
    public void ChiTieuDoanhNghiepKhongNamDuoc_PhaiDeTrong(int indicator)
    {
        // Điền 0 vào đây là khai thay NLĐ những thứ công ty không có căn cứ.
        var figures = Compute([StandardMonth()]);

        figures[indicator].Should().BeNull();
    }

    [Fact]
    public void SoNguoiPhuThuoc_UuTienSoDaKhaiTrenBangLuong()
    {
        // Bảng lương mới là căn cứ của chỉ tiêu [30]; [27] phải cùng nguồn để hai ô không đá nhau.
        var rows = new[]
        {
            Month((PayrollColumnCodes.SlNpt, 0m)),
            Month((PayrollColumnCodes.SlNpt, 2m)),
        };

        Compute(rows, dependents: 5)[27].Should().Be(2m);
    }

    [Fact]
    public void SoNguoiPhuThuoc_LayMucCaoNhatTrongNam()
    {
        var rows = new[]
        {
            Month((PayrollColumnCodes.SlNpt, 1m)),
            Month((PayrollColumnCodes.SlNpt, 3m)),
            Month((PayrollColumnCodes.SlNpt, 3m)),
        };

        Compute(rows)[27].Should().Be(3m);
    }

    [Fact]
    public void SoNguoiPhuThuoc_RoiVeHoSoKhiKyLuongKhongCoCotSlNpt()
    {
        Compute([StandardMonth()], dependents: 2)[27].Should().Be(2m);
    }
}
