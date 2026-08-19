using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ⭐ Phase 5+6 — đánh cờ STALE cho mọi kỳ lương còn <c>Draft</c>.
    ///
    /// <para>Engine đã đổi cách tính: mức lương lấy theo từng ĐOẠN hợp đồng, nền đóng BH trừ những
    /// ngày hợp đồng không đóng bảo hiểm, phụ lục lương/phụ cấp áp đúng từ ngày hiệu lực. Kỳ Draft
    /// đang có vẫn giữ số tính theo cách CŨ cho tới khi ai đó bấm "Tính lại" — chốt kỳ ngay lúc đó
    /// là chốt bằng số cũ mà không ai biết.</para>
    ///
    /// <para>Chỉ đụng kỳ <c>Draft</c> (Status = 1). Kỳ đã <c>Finalized</c>/<c>Locked</c> giữ nguyên:
    /// số của chúng đã đóng băng theo <c>FormulaSnapshotJson</c> + snapshot input, đúng như luật và
    /// cách tính tại thời điểm chốt.</para>
    ///
    /// <para><c>PayrollPeriod.Status</c> lưu dạng SỐ (<c>HasConversion&lt;int&gt;</c>) — khác
    /// <c>Contract.Status</c> vốn lưu chuỗi tên enum. So sai kiểu là migration chết ngay.</para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260819120000_MarkDraftPeriodsStaleAfterEngineChange")]
    public partial class MarkDraftPeriodsStaleAfterEngineChange : Migration
    {
        private const string Reason =
            "Cách tính lương đã đổi (lương theo đoạn hợp đồng, nền BH trừ ngày không đóng, " +
            "phụ lục áp từ ngày hiệu lực) — bấm Tính lại để cập nhật số.";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
UPDATE PayrollPeriods
   SET IsStale = 1,
       StaleReason = N'{Reason}',
       StaleMarkedAt = SYSUTCDATETIME(),
       UpdatedAt = SYSUTCDATETIME()
 WHERE Status = 1 AND IsStale = 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cố ý KHÔNG hạ cờ: không có nguồn nào để biết kỳ nào vốn đã stale từ trước vì lý do
            // khác (sửa chấm công, đổi hợp đồng...). Hạ hết là xoá mất cảnh báo thật của kế toán.
        }
    }
}
