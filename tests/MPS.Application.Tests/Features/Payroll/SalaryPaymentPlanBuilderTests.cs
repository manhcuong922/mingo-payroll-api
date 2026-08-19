using FluentAssertions;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Domain.Constants;
using EmployeeInformationEntity = MPS.Domain.Entities.EmployeeInformation;
using UserEntity = MPS.Domain.Entities.User;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using Xunit;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Luật quyết định ai được chuyển khoản lương, xếp vào bảng nào, ai bị loại.
/// Sai ở đây là chuyển nhầm tiền hoặc bỏ sót người — nên kiểm từng nhánh.
/// </summary>
public class SalaryPaymentPlanBuilderTests
{
    private const string InHouse = "VPBANK";

    private static readonly IReadOnlyDictionary<string, string> Provinces =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TP_HO_CHI_MINH"] = "TP. HO CHI MINH",
            ["HA_NOI"] = "HA NOI",
        };

    private static PayrollRow Row(
        string code,
        string name,
        decimal net,
        string? bankNumber = "1900123",
        string? bankCode = InHouse,
        string? branch = "CN Sai Gon",
        string? province = "TP_HO_CHI_MINH",
        int order = 0,
        PayrollRowType rowType = PayrollRowType.Employee)
        => new()
        {
            RowType = rowType,
            DisplayOrder = order,
            EmployeeInformation = new EmployeeInformationEntity
            {
                FullName = name,
                BankNumber = bankNumber,
                BankCode = bankCode,
                BankBranch = branch,
                BankProvinceCode = province,
                PermanentAddress = "123 Lê Lợi",
                User = new UserEntity { EmployeeCode = code },
            },
            Cells =
            [
                new PayrollCellValue
                {
                    ColumnCode = PayrollColumnCodes.ThucNhan,
                    ComputedValue = net,
                },
            ],
        };

    private static SalaryPaymentPlan Build(params PayrollRow[] rows)
        => SalaryPaymentPlanBuilder.Build(rows, InHouse, Provinces);

    [Fact]
    public void Build_TachNvTheoNganHangChuQuan()
    {
        var plan = Build(
            Row("NV01", "Nguyễn Văn Minh", 7_000_000m, bankCode: "VPBANK"),
            Row("NV02", "Trần Thị Hoa", 8_000_000m, bankCode: "VIETCOMBANK"));

        plan.InHouseBank.Should().ContainSingle().Which.FullName.Should().Be("Nguyễn Văn Minh");
        plan.OtherBanks.Should().ContainSingle().Which.FullName.Should().Be("Trần Thị Hoa");
        plan.Excluded.Should().BeEmpty();
    }

    [Fact]
    public void Build_MaNganHangKhongPhanBietHoaThuong()
    {
        var plan = Build(Row("NV01", "A", 1_000m, bankCode: "vpbank"));

        plan.InHouseBank.Should().HaveCount(1);
        plan.OtherBanks.Should().BeEmpty();
    }

    [Fact]
    public void Build_TongTienVaSoMonTinhTrenNvDuocChuyen()
    {
        var plan = Build(
            Row("NV01", "A", 7_000_000m, bankCode: "VPBANK"),
            Row("NV02", "B", 8_000_000m, bankCode: "ACB"),
            Row("NV03", "C", 0m));   // bị loại, không được cộng vào tổng

        plan.TotalCount.Should().Be(2);
        plan.TotalAmount.Should().Be(15_000_000m);
    }

    [Fact]
    public void Build_BoQuaDongTong()
    {
        var plan = Build(
            Row("NV01", "A", 7_000_000m),
            Row("", "TỔNG", 999_000_000m, rowType: PayrollRowType.Total));

        plan.TotalCount.Should().Be(1);
        plan.TotalAmount.Should().Be(7_000_000m);
    }

    [Fact]
    public void Build_GiuThuTuTheoDisplayOrder()
    {
        var plan = Build(
            Row("NV03", "C", 1_000m, order: 3),
            Row("NV01", "A", 1_000m, order: 1),
            Row("NV02", "B", 1_000m, order: 2));

        plan.InHouseBank.Select(b => b.FullName).Should().ContainInOrder("A", "B", "C");
    }

    // ===== Các lý do bị loại =====

    [Theory]
    [InlineData(0)]
    [InlineData(-500_000)]
    public void Build_ThucNhanKhongDuong_ThiLoai(decimal net)
    {
        var plan = Build(Row("NV01", "A", net));

        plan.TotalCount.Should().Be(0);
        plan.Excluded.Should().ContainSingle()
            .Which.Reason.Should().Be(SalaryPaymentPlanBuilder.ReasonNonPositiveNet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_ThieuSoTaiKhoan_ThiLoai(string? bankNumber)
    {
        var plan = Build(Row("NV01", "A", 7_000_000m, bankNumber: bankNumber));

        plan.Excluded.Should().ContainSingle()
            .Which.Reason.Should().Be(SalaryPaymentPlanBuilder.ReasonMissingAccount);
    }

    [Fact]
    public void Build_ThieuMaNganHang_ThiLoai()
    {
        var plan = Build(Row("NV01", "A", 7_000_000m, bankCode: null));

        plan.Excluded.Should().ContainSingle()
            .Which.Reason.Should().Be(SalaryPaymentPlanBuilder.ReasonMissingBankCode);
    }

    [Theory]
    [InlineData(null, "TP_HO_CHI_MINH")]
    [InlineData("CN Q1", null)]
    [InlineData(null, null)]
    public void Build_NganHangKhacMaThieuChiNhanhHoacTinh_ThiLoai(string? branch, string? province)
    {
        var plan = Build(Row("NV01", "A", 7_000_000m,
            bankCode: "VIETCOMBANK", branch: branch, province: province));

        plan.Excluded.Should().ContainSingle()
            .Which.Reason.Should().Be(SalaryPaymentPlanBuilder.ReasonMissingBranchInfo);
    }

    [Fact]
    public void Build_NganHangChuQuanThiKhongDoiChiNhanhVaTinh()
    {
        // Chuyển nội bộ cùng ngân hàng không cần chi nhánh/tỉnh — không được loại oan.
        var plan = Build(Row("NV01", "A", 7_000_000m,
            bankCode: InHouse, branch: null, province: null));

        plan.InHouseBank.Should().HaveCount(1);
        plan.Excluded.Should().BeEmpty();
    }

    [Fact]
    public void Build_ThucNhanBangKhongDuocBaoTruocLyDoThieuHoSo()
    {
        // NV nghỉ không lương cả tháng vốn chưa khai tài khoản: lý do đúng là "không có tiền
        // để chuyển", báo "thiếu tài khoản" sẽ khiến kế toán đi đòi hồ sơ vô ích.
        var plan = Build(Row("NV01", "A", 0m, bankNumber: null, bankCode: null));

        plan.Excluded.Should().ContainSingle()
            .Which.Reason.Should().Be(SalaryPaymentPlanBuilder.ReasonNonPositiveNet);
    }

    [Fact]
    public void Build_GhiNhanDuThongTinNvBiLoaiDeKeToanXuLyTay()
    {
        var plan = Build(Row("NV07", "Phạm Văn Tú", 5_500_000m, bankNumber: null));

        var excluded = plan.Excluded.Should().ContainSingle().Subject;
        excluded.EmployeeCode.Should().Be("NV07");
        excluded.FullName.Should().Be("Phạm Văn Tú");
        excluded.Amount.Should().Be(5_500_000m, "kế toán cần biết phải chi tay bao nhiêu");
    }

    // ===== Dữ liệu ghi lên chứng từ =====

    [Fact]
    public void Build_TraTenTinhTheoDinhDangNganHangYeuCau()
    {
        var plan = Build(Row("NV01", "A", 1_000m,
            bankCode: "ACB", province: "TP_HO_CHI_MINH"));

        plan.OtherBanks.Single().ProvinceFormName.Should().Be("TP. HO CHI MINH");
    }

    [Fact]
    public void Build_UuTienDiaChiLienLacHonThuongTru()
    {
        var row = Row("NV01", "A", 1_000m);
        row.EmployeeInformation!.ContactAddress = "456 Nguyễn Huệ";

        var plan = SalaryPaymentPlanBuilder.Build([row], InHouse, Provinces);

        plan.InHouseBank.Single().Address.Should().Be("456 Nguyễn Huệ");
    }

    [Fact]
    public void Build_LayGiaTriKeToanSuaTayChuKhongPhaiSoEngineTinh()
    {
        var row = Row("NV01", "A", 7_000_000m);
        row.Cells.First().IsOverridden = true;
        row.Cells.First().OverrideValue = 6_500_000m;

        var plan = SalaryPaymentPlanBuilder.Build([row], InHouse, Provinces);

        plan.TotalAmount.Should().Be(6_500_000m);
    }

    // ===== Số dòng cần chèn thêm =====

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(33, 30)]
    public void ExtraRows_ChiChenPhanVuotQuaSoDongKeSan(int employees, int expectedExtra)
    {
        var rows = Enumerable.Range(1, employees)
            .Select(i => Row($"NV{i:00}", $"NV {i}", 1_000_000m, order: i))
            .ToArray();

        var plan = Build(rows);

        plan.ExtraRowsForInHouse.Should().Be(expectedExtra);
    }
}
