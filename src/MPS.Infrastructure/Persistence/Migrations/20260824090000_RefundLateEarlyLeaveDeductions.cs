using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ⭐ LATE-EARLY-2026-08 — HOÀN TRẢ quỹ phép năm đã bị trừ bởi đơn đi muộn/về sớm.
    ///
    /// <para>Quyết định PO 24/08/2026 bỏ quota nội quy (2 lần/tháng, ≤30 phút/lần) và bỏ luôn đường
    /// trừ quỹ phép năm đi kèm nó, áp dụng HỒI TỐ: người đã bị trừ theo quy định cũ được cộng trả.
    /// Không đụng schema — chỉ sửa dữ liệu.</para>
    ///
    /// <para><b>Quy đổi phải soi gương đúng công thức đã trừ</b> (lớp <c>LateEarlyLeaveDeduction</c>
    /// nay đã xoá): <c>ngày = ROUND(phút / (60 × giờ_chuẩn_ngày), 2)</c>, trong đó giờ chuẩn/ngày lấy
    /// từ phiên bản <c>LaborLawConstantsVersions</c> hiệu lực TẠI NGÀY CỦA ĐƠN — cùng thứ tự ưu tiên
    /// với <c>LaborLawConstantsRepository.GetActiveAtAsync</c>: bản có cờ <c>IsActive</c> trước, không
    /// có thì bản chưa bật cờ có <c>ValidFrom</c> gần nhất; cả hai đều bỏ qua bản đã lưu trữ. Thiếu
    /// hẳn phiên bản (dữ liệu compliance chưa seed) thì lùi về 8 giờ — mặc định của entity.</para>
    ///
    /// <para><b>Kẹp sàn 0</b> khi cộng trả: quỹ có thể đã được HR chỉnh tay hoặc recompute lại sau
    /// lần trừ, cộng trả mù sẽ đẩy <c>UsedDays</c> xuống âm và làm hỏng phần "còn lại" trên màn hình
    /// nghỉ phép.</para>
    ///
    /// <para><b>Idempotent</b>: sau khi hoàn, cờ <c>DeductFromLeaveBalance</c> của chính đơn đó được
    /// hạ về 0 nên chạy lại không cộng trả lần hai — và chip "Trừ quỹ" trên màn hình đơn cũng không
    /// còn hiện sai. Dấu vết được nối vào <c>ReviewNote</c> khi còn đủ chỗ trong 500 ký tự.</para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260824090000_RefundLateEarlyLeaveDeductions")]
    public partial class RefundLateEarlyLeaveDeductions : Migration
    {
        private const string RefundNote = " [Đã hoàn quỹ phép — bỏ trừ quỹ theo QĐ 24/08/2026]";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
IF OBJECT_ID(N'[LateEarlyRequests]', N'U') IS NULL
    OR OBJECT_ID(N'[LeaveBalances]', N'U') IS NULL
    OR OBJECT_ID(N'[LeaveTypes]', N'U') IS NULL
    RETURN;

-- Số ngày phép đã bị trừ cho từng đơn, quy đổi bằng đúng công thức của lớp đã xoá.
WITH Deducted AS (
    SELECT
        r.[Id]                       AS RequestId,
        r.[EmployeeInformationId]    AS EmployeeInformationId,
        YEAR(r.[WorkDate])           AS BalanceYear,
        ROUND(
            r.[Minutes] / (60.0 * COALESCE((
                SELECT TOP 1 v.[StandardWorkingHoursPerDay]
                FROM [LaborLawConstantsVersions] v
                WHERE v.[IsActive] = 1 AND v.[IsArchived] = 0 AND v.[ValidFrom] <= r.[WorkDate]
                ORDER BY v.[ValidFrom] DESC
            ), (
                SELECT TOP 1 v2.[StandardWorkingHoursPerDay]
                FROM [LaborLawConstantsVersions] v2
                WHERE v2.[IsActive] = 0 AND v2.[IsArchived] = 0 AND v2.[ValidFrom] <= r.[WorkDate]
                ORDER BY v2.[ValidFrom] DESC
            ), 8.0)), 2)             AS Days
    FROM [LateEarlyRequests] r
    WHERE r.[DeductFromLeaveBalance] = 1
      AND r.[Status] = 2   -- LateEarlyRequestStatus.Approved
),
Totals AS (
    SELECT EmployeeInformationId, BalanceYear, SUM(Days) AS Days
    FROM Deducted
    GROUP BY EmployeeInformationId, BalanceYear
)
UPDATE b
SET b.[UsedDays] = CASE WHEN b.[UsedDays] - t.Days < 0 THEN 0 ELSE b.[UsedDays] - t.Days END,
    b.[UpdatedAt] = SYSUTCDATETIME()
FROM [LeaveBalances] b
INNER JOIN [LeaveTypes] lt ON lt.[Id] = b.[LeaveTypeId] AND lt.[Code] = 'ANNUAL'
INNER JOIN Totals t
        ON t.EmployeeInformationId = b.[EmployeeInformationId]
       AND t.BalanceYear = b.[Year];

-- Hạ cờ để chạy lại không cộng trả lần hai; ghi dấu vết khi ReviewNote còn chỗ.
UPDATE [LateEarlyRequests]
SET [DeductFromLeaveBalance] = 0,
    [ReviewNote] = CASE
        WHEN LEN(COALESCE([ReviewNote], N'')) + {RefundNote.Length} <= 500
            THEN COALESCE([ReviewNote], N'') + N'{RefundNote}'
        ELSE [ReviewNote]
    END
WHERE [DeductFromLeaveBalance] = 1 AND [Status] = 2;
");
        }

        /// <summary>
        /// Không trừ lại. Quy định mới không còn khái niệm "đơn đi muộn trừ quỹ phép", nên rollback
        /// đúng nghĩa là dựng lại một chế tài đã bị bãi bỏ — và cờ nguồn đã bị hạ nên cũng không còn
        /// căn cứ để tính lại chính xác. Muốn quay lui phải khôi phục từ bản sao lưu DB.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
