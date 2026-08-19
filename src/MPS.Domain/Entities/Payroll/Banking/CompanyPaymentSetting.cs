using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;

namespace MPS.Domain.Entities.Payroll.Banking;

/// <summary>
/// Cấu hình chi trả cấp công ty — nguồn của các ô "bên trả tiền" trên 2 chứng từ ngân hàng
/// (lệnh chi lương VPBank mẫu MB01, ủy nhiệm chi nộp BHXH mẫu Agribank DP004).
///
/// <para>Singleton: hệ thống chỉ giữ DUY NHẤT 1 bản ghi (xem
/// <c>CompanyPaymentSettingEntityConfiguration</c>). Field để nullable vì HR khai dần —
/// nhưng lệnh xuất chứng từ sẽ CHẶN nếu thiếu field bắt buộc, thay vì xuất file khuyết.</para>
///
/// <para>Không nhồi vào <see cref="ContractParty"/> vì bảng đó append-only theo mốc hiệu lực
/// và phục vụ hợp đồng lao động; đổi số tài khoản ngân hàng không phải là đổi pháp nhân.</para>
/// </summary>
[Auditable]
public class CompanyPaymentSetting
{
    public int Id { get; set; }

    // ===== Khối trả lương (mẫu MB01 — VPBank) =====

    /// <summary>Mã ngân hàng chủ quản trả lương (<see cref="BankCatalog.Code"/>). Quyết định
    /// NV nào xếp vào bảng "TK tại VPBank" và NV nào vào bảng "TK ngân hàng khác".</summary>
    public string? SalaryBankCode { get; set; }

    /// <summary>Chi nhánh nhận lệnh chi — ô "Kính gửi: ... Chi nhánh".</summary>
    public string? SalaryBankBranch { get; set; }

    /// <summary>Tài khoản trích nợ để chi lương và thu phí.</summary>
    public string? SalaryAccountNo { get; set; }

    /// <summary>Tên chủ tài khoản trích nợ (thường trùng tên pháp nhân).</summary>
    public string? SalaryAccountName { get; set; }

    /// <summary>Mã khách hàng doanh nghiệp tại ngân hàng (CIF) — ô "Mã KH (CIF)".</summary>
    public string? SalaryCustomerCif { get; set; }

    /// <summary>Điện thoại liên lạc in trên lệnh chi lương.</summary>
    public string? SalaryContactPhone { get; set; }

    // ===== Khối nộp BHXH (mẫu DP004 — Agribank) =====

    /// <summary>
    /// Mã đơn vị tham gia BHXH do cơ quan BHXH cấp cho doanh nghiệp (VD "TG14475").
    /// Là thành phần bắt buộc của nội dung chuyển khoản theo Công văn 1995/BHXH-TCKT.
    /// </summary>
    public string? BhxhUnitCode { get; set; }

    /// <summary>
    /// Mã cơ quan BHXH quản lý thu (VD "00107"). Cùng với <see cref="BhxhUnitCode"/> tạo nên
    /// nội dung chuyển khoản; ghi sai thì cơ quan BHXH không đối soát tự động được khoản nộp
    /// và quá trình đóng của NLĐ bị treo.
    /// </summary>
    public string? BhxhAgencyCode { get; set; }

    /// <summary>Tên cơ quan BHXH thụ hưởng — ô "Khách hàng thụ hưởng" trên UNC.</summary>
    public string? BhxhAgencyName { get; set; }

    /// <summary>Mã ngân hàng công ty dùng để nộp BHXH (<see cref="BankCatalog.Code"/>).</summary>
    public string? BhxhPayerBankCode { get; set; }

    /// <summary>Tên/chi nhánh ngân hàng bên trả tiền, in ở ô "Tại ngân hàng" phía Nợ.</summary>
    public string? BhxhPayerBankBranch { get; set; }

    /// <summary>Tài khoản công ty dùng để trích nộp BHXH.</summary>
    public string? BhxhPayerAccountNo { get; set; }

    /// <summary>Tài khoản thụ hưởng của cơ quan BHXH.</summary>
    public string? BhxhBeneficiaryAccountNo { get; set; }

    /// <summary>Ngân hàng giữ tài khoản của cơ quan BHXH.</summary>
    public string? BhxhBeneficiaryBankName { get; set; }

    // ===== Audit =====
    public DateTime CreatedAt { get; set; }
    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
