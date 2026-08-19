using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MPS.Infrastructure.Persistence;

/// <summary>
/// Địa chỉ email của TOÀN BỘ tài khoản demo — dồn về một hòm thư thật để test được luồng gửi mail
/// (đặt lại mật khẩu, thông báo duyệt đơn, phiếu lương...) mà không cần 40 hòm thư.
///
/// <para><b>Vì sao không đặt thẳng cùng một chuỗi cho mọi người.</b> <c>User.Email</c> có
/// <b>unique index</b> (<c>UserEntityConfiguration</c>) ⇒ gán trùng là <c>SaveChanges</c> ném ngay,
/// mà seed chạy trong <c>Program.cs</c> nên hậu quả là <b>API không khởi động nổi</b>.</para>
///
/// <para><b>Cách làm.</b> Plus-addressing <c>luanndhe180066+{username}@fpt.edu.vn</c>: Google
/// Workspace (nền tảng của mail FPT) bỏ qua phần sau dấu <c>+</c> khi định tuyến nên mọi thư vẫn về
/// đúng một hòm, đồng thời dòng <c>To:</c> cho biết thư phát sinh từ tài khoản nào.
/// <c>EmployeeInformation.Email</c> (không unique) dùng cùng địa chỉ để hai đường gửi thư của
/// <c>IUserEmailResolver</c> — email công ty và email cá nhân — cùng về một chỗ.</para>
/// </summary>
public sealed partial class DatabaseSeeder
{
    /// <summary>Hòm thư thật nhận toàn bộ mail của dữ liệu demo.</summary>
    private const string DemoMailboxLocalPart = "luanndhe180066";
    private const string DemoMailboxDomain = "fpt.edu.vn";

    /// <summary>Đuôi email của các bộ seed đời trước — dùng để nhận diện tài khoản demo cần đổi.</summary>
    private const string LegacyDemoEmailDomain = "@mingo.local";

    /// <summary>Email demo của một tài khoản, dẫn xuất xác định từ username.</summary>
    private static string DemoEmail(string username) =>
        $"{DemoMailboxLocalPart}+{username.ToLowerInvariant()}@{DemoMailboxDomain}";

    /// <summary>
    /// Đưa mọi tài khoản demo về hòm thư chung. Chạy CUỐI <c>SeedAsync</c> vì các pha trước còn tạo
    /// thêm người (contract-test, hồ sơ chưa có tài khoản, các case chặn cổng đủ liệu).
    ///
    /// <para>Chỉ đụng tài khoản còn mang đuôi <c>@mingo.local</c> của seed đời trước — nhân sự tạo
    /// qua API thật (email công ty/cá nhân có thật) không bị ghi đè.</para>
    /// </summary>
    private async Task NormalizeDemoEmailsAsync(CancellationToken ct)
    {
        var users = await _context.Users
            .Where(u => u.Email.EndsWith(LegacyDemoEmailDomain))
            .ToListAsync(ct);

        foreach (var user in users) user.Email = DemoEmail(user.Username);

        // Hồ sơ nhân sự: bám theo email của tài khoản; hồ sơ chưa gắn tài khoản thì dẫn xuất từ CCCD
        // (không có username để làm nhãn).
        var employees = await _context.EmployeeInformations
            .Where(e => e.Email != null && e.Email.EndsWith(LegacyDemoEmailDomain))
            .Include(e => e.User)
            .ToListAsync(ct);

        foreach (var emp in employees)
            emp.Email = emp.User is not null ? DemoEmail(emp.User.Username) : DemoEmail($"nv{emp.CitizenId}");

        if (users.Count == 0 && employees.Count == 0) return;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Chuyển {Users} tài khoản + {Employees} hồ sơ nhân sự demo về hòm thư {Mailbox} (plus-addressing).",
            users.Count, employees.Count, $"{DemoMailboxLocalPart}@{DemoMailboxDomain}");
    }
}
