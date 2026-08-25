using FluentAssertions;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Domain.Entities.Payroll.Banking;
using Xunit;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Cổng chặn xuất chứng từ khi cấu hình chi trả còn thiếu.
///
/// <para>Đây là chỗ DUY NHẤT quyết định "đủ hay chưa", dùng chung cho cả màn hình xem trước lẫn
/// lệnh xuất. Nếu hai bên tự kiểm riêng thì sẽ có lúc xem trước báo đủ mà bấm tải lại lỗi — kế
/// toán đã tưởng xong việc.</para>
///
/// <para>Ranh giới cần giữ: chỉ những ô mà THIẾU LÀ CHỨNG TỪ SAI mới được liệt kê. Tên chủ tài
/// khoản / mã CIF / điện thoại để trống thì bản in trống ô đó, ngân hàng vẫn nhận — đưa chúng vào
/// danh sách bắt buộc là chặn nhầm một chứng từ hợp lệ.</para>
/// </summary>
public sealed class PaymentSettingRequirementsTests
{
    /// <summary>Cấu hình đã khai đủ mọi ô bắt buộc của cả hai chứng từ.</summary>
    private static CompanyPaymentSetting Complete() => new()
    {
        SalaryBankCode = "VPBANK",
        SalaryBankBranch = "Chi nhánh Hà Nội",
        SalaryAccountNo = "190312345678",

        BhxhUnitCode = "TG14475",
        BhxhAgencyCode = "00107",
        BhxhAgencyName = "BHXH tỉnh Tiền Giang",
        BhxhPayerAccountNo = "1600201234567",
        BhxhPayerBankBranch = "Agribank CN Tiền Giang",
        BhxhBeneficiaryAccountNo = "3741101234567",
    };

    // ===== Lệnh chi lương =====

    [Fact(DisplayName = "Lệnh chi lương: khai đủ 3 ô bắt buộc → không thiếu gì")]
    public void SalaryOrder_Complete_HasNoMissingFields()
    {
        PaymentSettingRequirements.ForSalaryOrder(Complete()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Lệnh chi lương: chưa khai cấu hình lần nào → báo thiếu toàn bộ")]
    public void SalaryOrder_NullSetting_ReportsEverythingMissing()
    {
        PaymentSettingRequirements.ForSalaryOrder(null).Should().ContainSingle();
    }

    [Fact(DisplayName = "Lệnh chi lương: thiếu ngân hàng chủ quản → chặn (không chia được 2 bảng)")]
    public void SalaryOrder_MissingBankCode_IsReported()
    {
        var setting = Complete();
        setting.SalaryBankCode = null;

        PaymentSettingRequirements.ForSalaryOrder(setting)
            .Should().ContainSingle().Which.Should().Contain("Ngân hàng chủ quản");
    }

    [Theory(DisplayName = "Lệnh chi lương: ô chỉ để trang trí bản in thì KHÔNG chặn")]
    [InlineData("name")]
    [InlineData("cif")]
    [InlineData("phone")]
    public void SalaryOrder_OptionalFields_DoNotBlockExport(string field)
    {
        var setting = Complete();
        setting.SalaryAccountName = field == "name" ? null : "CÔNG TY MINGO";
        setting.SalaryCustomerCif = field == "cif" ? null : "CIF001";
        setting.SalaryContactPhone = field == "phone" ? null : "0912345678";

        PaymentSettingRequirements.ForSalaryOrder(setting).Should().BeEmpty();
    }

    [Fact(DisplayName = "Lệnh chi lương: chuỗi toàn khoảng trắng tính là chưa khai")]
    public void SalaryOrder_WhitespaceOnly_CountsAsMissing()
    {
        var setting = Complete();
        setting.SalaryAccountNo = "   ";

        PaymentSettingRequirements.ForSalaryOrder(setting)
            .Should().ContainSingle().Which.Should().Contain("Tài khoản trích nợ");
    }

    // ===== Ủy nhiệm chi nộp BHXH =====

    [Fact(DisplayName = "UNC BHXH: khai đủ 6 ô bắt buộc → không thiếu gì")]
    public void BhxhRemittance_Complete_HasNoMissingFields()
    {
        PaymentSettingRequirements.ForBhxhRemittance(Complete()).Should().BeEmpty();
    }

    [Fact(DisplayName = "UNC BHXH: thiếu mã đơn vị + mã cơ quan → báo cả hai")]
    public void BhxhRemittance_MissingCodes_ReportsBoth()
    {
        var setting = Complete();
        setting.BhxhUnitCode = null;
        setting.BhxhAgencyCode = null;

        // Hai mã này tạo nên nội dung chuyển khoản theo Công văn 1995/BHXH-TCKT; thiếu thì cơ quan
        // BHXH không đối soát tự động được khoản nộp và quá trình đóng của NLĐ bị treo.
        PaymentSettingRequirements.ForBhxhRemittance(setting).Should().HaveCount(2);
    }

    [Fact(DisplayName = "UNC BHXH: ngân hàng thụ hưởng KHÔNG bắt buộc (biểu mẫu không có ô đó)")]
    public void BhxhRemittance_BeneficiaryBankName_IsOptional()
    {
        var setting = Complete();
        setting.BhxhBeneficiaryBankName = null;

        PaymentSettingRequirements.ForBhxhRemittance(setting).Should().BeEmpty();
    }

    [Fact(DisplayName = "Hai chứng từ độc lập: khai đủ khối lương không mở khoá khối BHXH")]
    public void TheTwoDocuments_AreGatedIndependently()
    {
        var setting = Complete();
        setting.BhxhUnitCode = null;

        PaymentSettingRequirements.ForSalaryOrder(setting).Should().BeEmpty();
        PaymentSettingRequirements.ForBhxhRemittance(setting).Should().NotBeEmpty();
    }
}
