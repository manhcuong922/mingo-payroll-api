using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll.Banking;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Hồ sơ NGÂN HÀNG của nhân sự (ngân hàng · chi nhánh · tỉnh/TP) và cấu hình chi trả cấp công ty —
/// hai đầu vào của lệnh chi lương VPBank (mẫu MB01) và ủy nhiệm chi nộp BHXH (mẫu Agribank DP004).
///
/// <para><b>Vì sao phải seed thêm.</b> Các pha seed trước chỉ điền <c>BankNumber</c>; ba ô còn lại
/// (<c>BankCode</c>/<c>BankBranch</c>/<c>BankProvinceCode</c>) luôn trống. Mà
/// <c>SalaryPaymentPlanBuilder</c> LOẠI khỏi lệnh chi mọi NV chưa khai ngân hàng ⇒ mở màn hình
/// chứng từ chi trả ra chỉ thấy một bảng "bị loại" dài bằng cả công ty, không có dòng nào để chi.
/// Cấu hình cấp công ty còn nặng hơn: thiếu là lệnh xuất bị CHẶN thẳng
/// (<c>PaymentSettingRequirements</c>).</para>
///
/// <para><b>Phân bổ có chủ ý.</b> Đa số NV mở tài khoản tại chính ngân hàng trả lương (VPBank) —
/// đúng thực tế công ty mở tài khoản đồng loạt cho công nhân — số còn lại rải ra ngân hàng khác để
/// mẫu MB01 có dữ liệu ở CẢ HAI bảng ("TK tại VPBank" và "TK ngân hàng khác", hai bảng có bộ cột
/// khác nhau). Kèm 2 case lắt léo, xem chú thích ⭐ trong bảng phân bổ.</para>
///
/// <para><b>Idempotent.</b> Chỉ điền vào ô đang TRỐNG, không bao giờ đè hồ sơ đã khai (qua API hoặc
/// lần seed trước) — chạy lại seed không được phép thay số tài khoản của ai. Cấu hình cấp công ty
/// cũng chỉ tạo khi chưa có bản ghi nào (bảng là singleton).</para>
///
/// <para>Chạy CUỐI <c>SeedAsync</c>: mọi pha trên còn tạo người, gom về một chỗ thì không sót ai và
/// không phải rải logic ngân hàng ra 5 file seed.</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    /// <summary>Mã ngân hàng chủ quản trả lương của công ty (<see cref="BankCatalog.Code"/>).</summary>
    private const string InHouseSalaryBankCode = "VPBANK";

    /// <summary>Tài khoản ngân hàng của một NV — 3 ô ngoài số tài khoản.</summary>
    /// <param name="BankCode">Mã ngân hàng, phải khớp danh mục <c>BankCatalogs</c>.</param>
    /// <param name="Branch">Chi nhánh/PGD. Null = cố ý bỏ trống (xem ⭐ C-BANK-01).</param>
    /// <param name="ProvinceCode">Mã tỉnh/TP, phải khớp danh mục <c>ProvinceCatalogs</c>.</param>
    private sealed record SeedBankAccount(string BankCode, string? Branch, string? ProvinceCode);

    /// <summary>
    /// Đầu số tài khoản theo từng ngân hàng — để số tài khoản trên chứng từ trông đúng nhà băng của
    /// nó, thay vì cả công ty dùng chung một dải "1903…" của VPBank như seed cũ sinh ra.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> BankAccountPrefixes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VPBANK"] = "190312",
            ["VIETCOMBANK"] = "001100",
            ["TECHCOMBANK"] = "190212",
            ["BIDV"] = "211100",
            ["MB"] = "088800",
            ["AGRIBANK"] = "150720",
            ["VIETINBANK"] = "103000",
        };

    /// <summary>
    /// Phân bổ ngân hàng theo danh bạ nhân sự demo (key = Username, cùng key với
    /// <c>StaffRoster</c>). Người KHÔNG có trong bảng này (nhân sự nhóm contract-test, case ngoại lệ,
    /// hoặc hồ sơ tạo qua API thật) rơi về <see cref="DefaultBankAccount"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, SeedBankAccount> StaffBankAccounts =
        new Dictionary<string, SeedBankAccount>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Khối văn phòng ──
            ["director"]    = new("VPBANK",      "Chi nhánh Hà Nội",         "HA_NOI"),
            ["hr"]          = new("VPBANK",      "Chi nhánh Hà Đông",        "HA_NOI"),
            ["accountant"]  = new("VPBANK",      "Chi nhánh Hà Đông",        "HA_NOI"),
            ["tpkd"]        = new("TECHCOMBANK", "Chi nhánh Thanh Xuân",     "HA_NOI"),

            // ── Xưởng 1: Khung tranh ──
            ["quandoc_x1"]  = new("VPBANK",      "Chi nhánh Hà Đông",        "HA_NOI"),
            ["totruong_x1"] = new("VPBANK",      "Chi nhánh Hà Đông",        "HA_NOI"),
            ["cn_x1_1"]     = new("VPBANK",      "PGD Thanh Oai",            "HA_NOI"),
            ["cn_x1_2"]     = new("VPBANK",      "PGD Thanh Oai",            "HA_NOI"),
            ["cn_x1_3"]     = new("VIETCOMBANK", "Chi nhánh Hà Tây",         "HA_NOI"),
            ["cn_x1_4"]     = new("VPBANK",      "PGD Thanh Oai",            "HA_NOI"),
            ["cn_x1_5"]     = new("AGRIBANK",    "Chi nhánh Thanh Oai",      "HA_NOI"),
            ["cn_x1_6"]     = new("VPBANK",      "PGD Thanh Oai",            "HA_NOI"),

            // ── Xưởng 2: Nội thất ──
            ["quandoc_x2"]  = new("VPBANK",      "Chi nhánh Hà Đông",        "HA_NOI"),
            ["totruong_x2"] = new("MB",          "Chi nhánh Nam Từ Liêm",    "HA_NOI"),
            ["cn_x2_1"]     = new("VPBANK",      "PGD Hoài Đức",             "HA_NOI"),
            ["cn_x2_2"]     = new("BIDV",        "Chi nhánh Hoài Đức",       "HA_NOI"),
            ["cn_x2_3"]     = new("VPBANK",      "PGD Hoài Đức",             "HA_NOI"),
            ["cn_x2_4"]     = new("VIETINBANK",  "Chi nhánh Nam Thăng Long", "HA_NOI"),
            ["cn_x2_5"]     = new("VPBANK",      "PGD Hoài Đức",             "HA_NOI"),
            ["cn_x2_6"]     = new("VPBANK",      "PGD Hoài Đức",             "HA_NOI"),

            // ── Xưởng 3: Cơ khí ──
            ["quandoc_x3"]  = new("VPBANK",      "Chi nhánh Hà Đông",        "HA_NOI"),
            ["totruong_x3"] = new("VPBANK",      "Chi nhánh Hà Đông",        "HA_NOI"),
            ["cn_x3_1"]     = new("VPBANK",      "PGD Thanh Trì",            "HA_NOI"),
            ["cn_x3_2"]     = new("TECHCOMBANK", "Chi nhánh Thanh Trì",      "HA_NOI"),
            // ⭐ C-BANK-01 — TK ở ngân hàng KHÁC nhưng bỏ trống chi nhánh/tỉnh. Mẫu MB01 bắt buộc 2 ô
            // này với nhóm ngoài VPBank, thiếu là ngân hàng trả lại CẢ lệnh chi ⇒ builder loại riêng
            // người này kèm lý do. Đây là dữ liệu để nhìn thấy bảng "NV bị loại" hoạt động thật.
            // Cổng đủ liệu của kỳ lương KHÔNG kiểm 3 ô này (chỉ kiểm số TK + MST) nên case này không
            // chặn việc tạo kỳ lương demo.
            ["cn_x3_3"]     = new("VIETCOMBANK", null,                       null),
            ["cn_x3_4"]     = new("VPBANK",      "PGD Thanh Trì",            "HA_NOI"),
            // ⭐ C-BANK-02 — công nhân giữ tài khoản mở ở quê: tỉnh/TP khác nơi công ty đóng trụ sở,
            // để cột "Tỉnh/TP" trên mẫu MB01 không phải chỉ có mỗi "HA NOI".
            ["cn_x3_5"]     = new("AGRIBANK",    "Chi nhánh Thanh Hóa",      "THANH_HOA"),
            ["cn_x3_6"]     = new("VPBANK",      "PGD Thanh Trì",            "HA_NOI"),
        };

    /// <summary>
    /// Mặc định cho người ngoài danh bạ: tài khoản mở tại chính ngân hàng trả lương. Cố ý chọn
    /// ngân hàng chủ quản chứ không rải ngẫu nhiên — nhóm này (contract-test, case ngoại lệ) sinh ra
    /// để test luồng hợp đồng, cho vào bảng "TK tại VPBank" là gọn nhất và không bao giờ bị loại vì
    /// thiếu chi nhánh.
    /// </summary>
    private static readonly SeedBankAccount DefaultBankAccount =
        new(InHouseSalaryBankCode, "Chi nhánh Hà Đông", "HA_NOI");

    private async Task SeedEmployeeBankAccountsAsync(CancellationToken ct)
    {
        await SeedCompanyPaymentSettingAsync(ct);
        await FillEmployeeBankAccountsAsync(ct);
    }

    /// <summary>
    /// Cấu hình chi trả cấp công ty (singleton). Chỉ tạo khi bảng còn rỗng: đây là dữ liệu kế toán
    /// sẽ sửa qua màn hình cấu hình, seed đè lên là xoá mất số tài khoản thật của công ty.
    /// </summary>
    private async Task SeedCompanyPaymentSettingAsync(CancellationToken ct)
    {
        if (await _context.CompanyPaymentSettings.AnyAsync(ct))
        {
            _logger.LogDebug("CompanyPaymentSetting đã có; bỏ qua seed.");
            return;
        }

        var adminId = await _context.Users.AsNoTracking()
            .Where(u => u.RoleId == SystemRoles.SystemAdminId)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

        var setting = new CompanyPaymentSetting
        {
            // ── Khối trả lương (mẫu MB01 — VPBank) ──
            SalaryBankCode = InHouseSalaryBankCode,
            SalaryBankBranch = "Chi nhánh Hà Đông",
            SalaryAccountNo = "190312000000001",
            // Tên chủ TK in trên lệnh chi là bản KHÔNG DẤU — ngân hàng đối chiếu theo chuỗi này.
            SalaryAccountName = "CONG TY TNHH SAN XUAT VA THUONG MAI MINGO",
            SalaryCustomerCif = "1487205",
            SalaryContactPhone = "02466778899",

            // ── Khối nộp BHXH (mẫu DP004 — Agribank) ──
            // Mã đơn vị + mã cơ quan thu là 2 thành phần BẮT BUỘC của nội dung chuyển khoản theo
            // Công văn 1995/BHXH-TCKT; sai là khoản nộp không được đối soát tự động.
            BhxhUnitCode = "TG14475",
            BhxhAgencyCode = "00107",
            BhxhAgencyName = "Bảo hiểm xã hội huyện Thanh Oai, TP Hà Nội",
            BhxhPayerBankCode = "AGRIBANK",
            BhxhPayerBankBranch = "Agribank Chi nhánh huyện Thanh Oai",
            BhxhPayerAccountNo = "1507205012345",
            BhxhBeneficiaryAccountNo = "1507201068888",
            BhxhBeneficiaryBankName = "Agribank Chi nhánh Hà Tây",

            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = adminId,
        };

        await _context.CompanyPaymentSettings.AddAsync(setting, ct);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Seeded cấu hình chi trả cấp công ty (ngân hàng trả lương {Bank}).", InHouseSalaryBankCode);
    }

    private async Task FillEmployeeBankAccountsAsync(CancellationToken ct)
    {
        var employees = await _context.EmployeeInformations
            .Include(e => e.User)
            .ToListAsync(ct);

        var filled = 0;
        foreach (var emp in employees)
        {
            // Hồ sơ chưa có số tài khoản là case CỐ Ý (R3 — cổng đủ liệu phải bắt được nó).
            // Tự điền hộ ở đây là xoá mất chính thứ đang cần test.
            if (string.IsNullOrWhiteSpace(emp.BankNumber)) continue;
            // Đã khai ngân hàng (nhập tay qua API hoặc lần seed trước) ⇒ không đụng vào.
            if (!string.IsNullOrWhiteSpace(emp.BankCode)) continue;

            var username = emp.User?.Username;
            var account = username is not null && StaffBankAccounts.TryGetValue(username, out var mapped)
                ? mapped
                : DefaultBankAccount;

            emp.BankCode = account.BankCode;
            emp.BankBranch = account.Branch;
            emp.BankProvinceCode = account.ProvinceCode;
            emp.BankNumber = RebrandPlaceholderAccountNo(emp, account.BankCode);
            filled++;
        }

        if (filled > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Đã điền tài khoản ngân hàng cho {Count} hồ sơ nhân sự.", filled);
    }

    /// <summary>
    /// Đổi đầu số tài khoản cho khớp ngân hàng vừa gán — CHỈ khi số hiện tại đúng là chuỗi placeholder
    /// do chính seeder sinh ra (<c>190_300_000_000 + Id</c>, tính theo UserId hoặc theo Id hồ sơ tuỳ
    /// pha seed nào sinh). Số do người dùng nhập thật thì giữ nguyên: seed không có quyền sửa số tài
    /// khoản của ai.
    /// </summary>
    private static string RebrandPlaceholderAccountNo(
        Domain.Entities.EmployeeInformation employee, string bankCode)
    {
        var current = employee.BankNumber!.Trim();
        var placeholders = new[]
        {
            $"{190_300_000_000L + (employee.UserId ?? 0)}",
            $"{190_300_000_000L + employee.Id}",
        };

        if (!placeholders.Contains(current, StringComparer.Ordinal)) return current;

        return BankAccountPrefixes.TryGetValue(bankCode, out var prefix)
            ? $"{prefix}{employee.Id:D6}"
            : current;
    }
}
