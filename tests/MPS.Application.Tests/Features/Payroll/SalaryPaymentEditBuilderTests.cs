using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Domain.Entities.Payroll.Banking;
using Xunit;
using Cells = MPS.Application.Features.Payroll.PaymentDocuments.Common.SalaryPaymentFormCells;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Phép tính toạ độ sau khi chèn dòng — chỗ dễ sai nhất của lệnh chi lương.
/// Lệch một dòng là số tiền của người này rơi vào dòng của người khác, hoặc dòng tổng
/// đè lên dữ liệu nhân viên.
/// </summary>
public class SalaryPaymentEditBuilderTests
{
    private static SalaryPaymentBeneficiary Beneficiary(int i, string bank = "VPBANK")
        => new(
            EmployeeCode: $"NV{i:00}",
            FullName: $"Nhân viên {i}",
            AccountNo: $"19001{i:000}",
            Amount: 1_000_000m * i,
            Address: "123 Lê Lợi",
            BankCode: bank,
            BankBranch: "CN Sài Gòn",
            ProvinceFormName: "TP. HO CHI MINH");

    private static SalaryPaymentPlan Plan(int inHouse, int others)
        => new(
            Enumerable.Range(1, inHouse).Select(i => Beneficiary(i)).ToList(),
            Enumerable.Range(1, others).Select(i => Beneficiary(i, "ACB")).ToList(),
            []);

    private static CompanyPaymentSetting Setting() => new()
    {
        SalaryBankCode = "VPBANK",
        SalaryBankBranch = "CN Sài Gòn",
        SalaryAccountNo = "1900123456789",
        SalaryAccountName = "CONG TY TNHH MINGO",
        SalaryCustomerCif = "CIF00123",
        SalaryContactPhone = "02838222333",
    };

    private static IReadOnlyList<XlsxCellEdit> Edits(SalaryPaymentPlan plan)
        => SalaryPaymentEditBuilder.BuildEdits(
            plan, Setting(),
            new SalaryPaymentCompanyProfile("CÔNG TY TNHH MINGO", "2A Nguyễn Thị Minh Khai"),
            2026, 6, new DateTime(2026, 7, 5));

    private static string? TextAt(IReadOnlyList<XlsxCellEdit> edits, string cell)
        => edits.FirstOrDefault(e => e.Cell == cell)?.Text;

    private static decimal? NumberAt(IReadOnlyList<XlsxCellEdit> edits, string cell)
        => edits.FirstOrDefault(e => e.Cell == cell)?.Number;

    // ===== Số dòng cần chèn =====

    [Fact]
    public void BuildInsertions_VuaDuDongKeSan_ThiKhongChen()
    {
        SalaryPaymentEditBuilder.BuildInsertions(Plan(3, 3)).Should().BeEmpty();
    }

    [Fact]
    public void BuildInsertions_ChenDungSoDongThieuChoTungBang()
    {
        var insertions = SalaryPaymentEditBuilder.BuildInsertions(Plan(10, 5));

        insertions.Should().HaveCount(2);

        var table1 = insertions.Single(i => i.BeforeRow == Cells.Table1LastPrebakedRow + 1);
        table1.Count.Should().Be(7);
        table1.CloneStyleFromRow.Should().Be(Cells.Table1LastPrebakedRow);

        var table2 = insertions.Single(i => i.BeforeRow == Cells.Table2LastPrebakedRow + 1);
        table2.Count.Should().Be(2);
        table2.CloneStyleFromRow.Should().Be(Cells.Table2LastPrebakedRow);
    }

    [Fact]
    public void BuildInsertions_ToaDoKhaiTheoBieuMauGoc_KhongTuCongDon()
    {
        // Lớp filler áp dụng từ dưới lên, nên toạ độ bảng 2 vẫn là 38 dù bảng 1 chèn thêm 7 dòng.
        var insertions = SalaryPaymentEditBuilder.BuildInsertions(Plan(10, 10));

        insertions.Select(i => i.BeforeRow).Should().BeEquivalentTo([33u, 38u]);
    }

    // ===== Toạ độ bảng =====

    [Fact]
    public void BuildEdits_KhongChenDong_ThiBang2NamDungChoCu()
    {
        var edits = Edits(Plan(2, 2));

        TextAt(edits, Cells.ColFullName + Cells.Table1FirstDataRow).Should().Be("Nhân viên 1");
        TextAt(edits, Cells.ColFullName + Cells.Table2FirstDataRow).Should().Be("Nhân viên 1");
    }

    [Fact]
    public void BuildEdits_ChenDongBang1_ThiBang2TroiXuongDungBangSoDongChen()
    {
        // 10 NV nội bộ ⇒ chèn 7 dòng ⇒ bảng 2 từ dòng 35 xuống 42.
        var edits = Edits(Plan(10, 2));

        TextAt(edits, Cells.ColFullName + "42").Should().Be("Nhân viên 1");
        TextAt(edits, Cells.ColFullName + "43").Should().Be("Nhân viên 2");
    }

    [Fact]
    public void BuildEdits_NvNoiBoGhiLienTucTuDongDauBang()
    {
        var edits = Edits(Plan(5, 0));

        for (var i = 0; i < 5; i++)
        {
            var row = Cells.Table1FirstDataRow + (uint)i;
            TextAt(edits, Cells.ColFullName + row).Should().Be($"Nhân viên {i + 1}");
            NumberAt(edits, Cells.ColOrdinal + row).Should().Be(i + 1);
        }
    }

    // ===== Dòng tổng cộng =====

    [Fact]
    public void BuildEdits_DongTongTroiXuongTheoTongSoDongChenCuaCaHaiBang()
    {
        // 10 nội bộ (+7) và 6 ngân hàng khác (+3) ⇒ dòng tổng 38 xuống 48, 39 xuống 49.
        var plan = Plan(10, 6);
        var edits = Edits(plan);

        NumberAt(edits, "B48").Should().Be(plan.TotalAmount);
        TextAt(edits, "D48").Should().StartWith(Cells.TotalCountWords.Label);
        TextAt(edits, "D49").Should().StartWith(Cells.TotalAmountWords.Label);
    }

    [Fact]
    public void BuildEdits_DongTongKhongDeLenDongCuoiCuaBang2()
    {
        var plan = Plan(10, 6);
        var edits = Edits(plan);

        // Dòng NV cuối của bảng 2: 35 + 7 (chèn bảng 1) + 5 = 47 ⇒ dòng tổng phải là 48.
        TextAt(edits, Cells.ColFullName + "47").Should().Be("Nhân viên 6");

        // Cột B ở dòng 48 là ô TỔNG TIỀN (số), không được là tên một nhân viên nào.
        TextAt(edits, "B48").Should().BeNull();
        NumberAt(edits, "B48").Should().Be(plan.TotalAmount);

        // Và không có dòng NV nào bị đẩy xuống quá dòng tổng.
        var nameRows = edits
            .Where(e => e.Text != null && e.Text.StartsWith("Nhân viên"))
            .Select(e => int.Parse(e.Cell.Substring(1)))
            .ToList();

        nameRows.Should().NotBeEmpty();
        nameRows.Max().Should().BeLessThanOrEqualTo(47);
    }

    [Fact]
    public void BuildEdits_SoMonDocThanhChuKhongKemDonViTien()
    {
        var edits = Edits(Plan(10, 3));

        TextAt(edits, "D45").Should().Be("Số món (Bằng chữ): Mười ba món");
    }

    [Fact]
    public void BuildEdits_TongTienBangSoVaBangChuKhopNhau()
    {
        var plan = Plan(3, 0);   // 1tr + 2tr + 3tr
        var edits = Edits(plan);

        plan.TotalAmount.Should().Be(6_000_000m);
        NumberAt(edits, Cells.TotalAmountCell).Should().Be(6_000_000m);
        TextAt(edits, Cells.TotalAmountWords.Cell)
            .Should().Be("Số tiền (Bằng chữ): Sáu triệu đồng");
        TextAt(edits, Cells.TotalFigures.Cell).Should().Be("Bằng số: 6,000,000");
        TextAt(edits, Cells.TotalWords.Cell).Should().Be("Bằng chữ: Sáu triệu đồng");
    }

    // ===== Khối đầu chứng từ =====

    [Fact]
    public void BuildEdits_GhiLaiCaNhanLanGiaTriVaoOChungCuaBieuMau()
    {
        var edits = Edits(Plan(1, 0));

        TextAt(edits, Cells.CompanyName.Cell).Should().Be("Tên Doanh nghiệp*: CONG TY TNHH MINGO");
        TextAt(edits, Cells.CompanyAddress.Cell).Should().Be("Địa chỉ*: 2A Nguyễn Thị Minh Khai");
        TextAt(edits, Cells.CustomerCif.Cell).Should().Be("Mã KH (CIF): CIF00123");
        TextAt(edits, Cells.DebitAccount.Cell)
            .Should().Be("Tài khoản trích nợ và thu phí: 1900123456789");
        TextAt(edits, Cells.Currency.Cell).Should().Be("Loại tiền: VND");
        TextAt(edits, Cells.DocumentNo.Cell).Should().Be("Số chứng từ*: BL-06/2026");
        TextAt(edits, Cells.ValueDate.Cell)
            .Should().Be("Ngày Lệnh chi lương có giá trị thanh toán*: 05/07/2026");
    }

    [Fact]
    public void BuildEdits_ChiBang2MoiGhiChiNhanhVaTinh()
    {
        // Mẫu chỉ chừa cột chi nhánh/tỉnh cho bảng 2; ghi vào bảng 1 là đè lên vùng gộp F:H.
        var edits = Edits(Plan(2, 2));

        edits.Where(e => e.Cell == Cells.ColBranch + Cells.Table1FirstDataRow).Should().BeEmpty();
        edits.Where(e => e.Cell == Cells.ColProvince + Cells.Table1FirstDataRow).Should().BeEmpty();

        TextAt(edits, Cells.ColBranch + Cells.Table2FirstDataRow).Should().Be("CN Sài Gòn");
        TextAt(edits, Cells.ColProvince + Cells.Table2FirstDataRow).Should().Be("TP. HO CHI MINH");
    }

    [Fact]
    public void BuildEdits_MoiOChiDuocGhiMotLan()
    {
        // Trùng ô nghĩa là hai giá trị tranh nhau một chỗ — giá trị nào thắng là ngẫu nhiên.
        var edits = Edits(Plan(10, 6));

        edits.Select(e => e.Cell).Should().OnlyHaveUniqueItems();
    }
}
