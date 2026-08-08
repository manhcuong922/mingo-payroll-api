using MPS.Domain.Attributes;
using MPS.Domain.Interfaces;

namespace MPS.Domain.Entities.Compliance;

[Auditable]
public sealed class SocialInsuranceRateVersion : IComplianceVersion
{
    public int Id { get; set; }
    public DateOnly ValidFrom { get; set; }
    public bool IsActive { get; set; }
    public bool IsArchived { get; set; }

    [Obsolete("Dùng Funds (SocialInsuranceFundEntry, FundCode=Ht) thay thế — giữ cột cho backward compat, không migrate dữ liệu cũ.")]
    public decimal EmployeeSocialPercent { get; set; }
    [Obsolete("Dùng Funds (SocialInsuranceFundEntry) thay thế — giữ cột cho backward compat, không migrate dữ liệu cũ.")]
    public decimal EmployerSocialPercent { get; set; }
    [Obsolete("Dùng Funds (SocialInsuranceFundEntry, FundCode=TnldBnn) thay thế — giữ cột cho backward compat, không migrate dữ liệu cũ.")]
    public decimal EmployerOccupationalDiseasePercent { get; set; }
    public decimal EmployeeHealthPercent { get; set; }
    public decimal EmployerHealthPercent { get; set; }
    public decimal EmployeeUnemploymentPercent { get; set; }
    public decimal EmployerUnemploymentPercent { get; set; }
    /// <summary>KPCĐ — kinh phí công đoàn DN trích nộp (Luật Công đoàn, NĐ 191/2013): 2% quỹ lương.</summary>
    public decimal EmployerUnionFundPercent { get; set; }

    /// <summary>
    /// Đoàn phí công đoàn NLĐ là ĐOÀN VIÊN đóng (QĐ 1908/QĐ-TLĐ Đ.23): 1% tiền lương làm căn cứ
    /// đóng BHXH. Khác <see cref="EmployerUnionFundPercent"/> — khoản kia DN nộp cho MỌI NLĐ,
    /// khoản này chỉ thu của người có tham gia công đoàn
    /// (<c>EmployeeInformation.IsUnionMember</c>).
    /// </summary>
    public decimal EmployeeUnionDuesPercent { get; set; } = 1.00m;

    /// <summary>
    /// Trần đoàn phí theo QĐ 1908/QĐ-TLĐ Đ.23: tối đa 10% <see cref="ReferenceAmount"/> (mức lương
    /// cơ sở). Lưu ý trần này chặn trên SỐ TIỀN đóng, khác trần khối BHXH/BHYT/BHTN vốn chặn trên
    /// NỀN đóng — xem công thức cột PHICONGDOANVIEN. Đặt 0 = không áp trần.
    /// </summary>
    public decimal EmployeeUnionDuesCapPercent { get; set; } = 10.00m;

    public decimal ReferenceAmount { get; set; }
    public int SocialCapMultiplier { get; set; } = 20;
    public int HealthCapMultiplier { get; set; } = 20;
    public int UnemploymentCapMultiplier { get; set; } = 20;

    public string EligibilityMode { get; set; } = string.Empty;
    public string? EligibleContractSubtypesCsv { get; set; }
    public int? MinTenureMonths { get; set; }

    public string LegalReference { get; set; } = string.Empty;
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public ICollection<SocialInsuranceFundEntry> Funds { get; set; } = [];
}
