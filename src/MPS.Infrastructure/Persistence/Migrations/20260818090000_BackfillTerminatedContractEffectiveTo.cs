using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 4 — backfill <c>Contracts.EffectiveTo</c> cho các HĐ ĐÃ chấm dứt trước khi
    /// <c>ContractTerminationRepository</c> biết đóng khoảng hiệu lực.
    ///
    /// <para>Trước bản sửa này, chấm dứt HĐ chỉ đổi <c>Status</c>. Với HĐ không xác định thời hạn
    /// (<c>EffectiveTo IS NULL</c>) thì hợp đồng vẫn "phủ" mọi ngày sau khi NLĐ đã nghỉ ⇒ (1) chấm
    /// công lỡ nhập sau ngày nghỉ vẫn được tính công, (2) tháng kế tiếp người đã nghỉ vẫn lọt cổng
    /// readiness với lỗi "chưa có chấm công" và chặn tạo kỳ lương của cả công ty.</para>
    ///
    /// <para><b>Không đụng schema</b> — chỉ vá dữ liệu. Idempotent: chạy lại không đổi thêm hàng nào
    /// vì điều kiện WHERE tự loại các hàng đã đúng.</para>
    ///
    /// <para><b>Không bao giờ kéo dài hợp đồng</b>: HĐ có hạn kết thúc TRƯỚC ngày chấm dứt giữ nguyên
    /// mốc của chính nó (điều kiện so sánh chỉ rút ngắn, không nới ra).</para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260818090000_BackfillTerminatedContractEffectiveTo")]
    public partial class BackfillTerminatedContractEffectiveTo : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE c
SET    c.EffectiveTo = t.TerminationDate,
       c.UpdatedAt   = SYSUTCDATETIME()
FROM   Contracts c
       INNER JOIN ContractTerminations t ON t.ContractId = c.Id
WHERE  c.EffectiveTo IS NULL
    OR CAST(c.EffectiveTo AS date) > CAST(t.TerminationDate AS date);
");
        }

        /// <summary>
        /// Không rollback được: giá trị <c>EffectiveTo</c> trước khi vá (NULL hay một mốc muộn hơn)
        /// không được lưu ở đâu cả. Việc vá cũng chỉ đưa dữ liệu về đúng thoả thuận trên quyết định
        /// chấm dứt nên không có lý do nghiệp vụ nào để quay lại.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
