using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 7 — NĐ 253/2026/NĐ-CP nâng NGƯỠNG khấu trừ thuế khoán 10% mỗi lần chi trả từ
    /// <b>2.000.000</b> lên <b>5.000.000</b> (hiệu lực 01/07/2026). Dựng bản
    /// <c>PitRegulationVersion</c> mới cho DB ĐÃ CÓ DỮ LIỆU (production), nơi <c>DatabaseSeeder</c>
    /// không bao giờ chạm tới vì <c>SeedPitRegulationAsync</c> mở đầu bằng
    /// <c>if (await _context.PitRegulationVersions.AnyAsync()) return;</c> mà prod đặt
    /// <c>SEED_RESET_ON_STARTUP=false</c>.
    ///
    /// <para>Cùng khuôn hai chốt của <c>BackfillOvertimeTaxExemptionCompliance</c>:</para>
    /// <list type="bullet">
    ///   <item><b>Chỉ chạy khi bảng ĐÃ có dữ liệu</b> — trên DB sạch (local/CI) migration chạy TRƯỚC
    ///     seeder, chèn ở đây sẽ làm seeder thấy "đã có dữ liệu" rồi bỏ qua toàn bộ phần PIT.</item>
    ///   <item><b>Chỉ chạy khi chưa có bản 01/07/2026</b> ⇒ idempotent.</item>
    /// </list>
    ///
    /// <para><b>Bản MỚI chứ không UPDATE tại chỗ:</b> kỳ lương trả trước 01/07/2026 tính lại vẫn
    /// phải ra ngưỡng 2tr của luật cũ — <c>GetActiveAtAsync</c> lọc <c>ValidFrom &lt;= ngày trả</c>
    /// nên tự chọn đúng bản theo thời điểm. Sửa tại chỗ là hồi tố luật mới lên số liệu cũ.</para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260819100000_PitFlatTaxThresholdFiveMillion")]
    public partial class PitFlatTaxThresholdFiveMillion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== 1. Bổ sung nội dung thuế khoán vào văn bản NĐ 253/2026 =====
            // Văn bản đã có từ migration 20260810160000 nhưng tiêu đề chỉ ghi phần phụ cấp ăn ca;
            // bản PIT dưới đây link vào chính văn bản này nên tiêu đề phải nói đủ hai nội dung.
            migrationBuilder.Sql(@"
UPDATE LegalDocuments
   SET Title = N'Ngưỡng phụ cấp ăn ca/ăn trưa miễn thuế 1,200,000 + ngưỡng khấu trừ thuế khoán 10% lên 5,000,000 từ 1/7/2026'
 WHERE Code = N'ND-253-2026'
   AND Title = N'Ngưỡng phụ cấp ăn ca/ăn trưa miễn thuế 1,200,000 từ 1/7/2026';");

            // ===== 2. Bản thuế TNCN hiệu lực 01/07/2026 =====
            // Sao chép nguyên giảm trừ gia cảnh + biểu luỹ tiến của bản đang áp dụng, CHỈ đổi ngưỡng
            // khấu trừ khoán — để không ghi đè số kế toán đã chỉnh tay trên prod.
            migrationBuilder.Sql(@"
DECLARE @admin INT = (SELECT TOP 1 Id FROM Users WHERE RoleId = 1 ORDER BY Id);

IF @admin IS NOT NULL
   AND EXISTS (SELECT 1 FROM PitRegulationVersions)
   AND NOT EXISTS (SELECT 1 FROM PitRegulationVersions WHERE ValidFrom = '2026-07-01')
BEGIN
    DECLARE @src INT = (
        SELECT TOP 1 Id FROM PitRegulationVersions
        WHERE IsArchived = 0 AND ValidFrom <= '2026-07-01'
        ORDER BY IsActive DESC, ValidFrom DESC);

    IF @src IS NOT NULL
    BEGIN
        -- PHẢI hạ cờ bản cũ TRƯỚC khi chèn: IX_PitRegulationVersions_IsActive là unique index lọc
        -- (IsActive=1 AND IsArchived=0) ⇒ chỉ được tồn tại ĐÚNG 1 hàng active. Chèn trước rồi mới
        -- hạ cờ sẽ vi phạm index ⇒ migration fail ⇒ API không khởi động được.
        UPDATE PitRegulationVersions
           SET IsActive = 0, UpdatedAt = SYSUTCDATETIME()
         WHERE IsActive = 1 AND IsArchived = 0;

        INSERT INTO PitRegulationVersions
            (ValidFrom, IsActive, IsArchived, PersonalDeductionAmount, DependentDeductionAmount,
             ShortTermContractMaxMonths, ShortTermFlatRatePercent, ShortTermTaxExemptMonthlyIncome,
             LegalReference, CreatedAt, CreatedByUserId)
        SELECT
             '2026-07-01', 1, 0, PersonalDeductionAmount, DependentDeductionAmount,
             ShortTermContractMaxMonths, ShortTermFlatRatePercent, 5000000,
             N'NĐ 253/2026/NĐ-CP — ngưỡng khấu trừ thuế khoán 10% lên 5.000.000đ/lần chi trả từ 01/07/2026',
             SYSUTCDATETIME(), CreatedByUserId
        FROM PitRegulationVersions WHERE Id = @src;

        DECLARE @newPit INT = CAST(SCOPE_IDENTITY() AS INT);

        -- Biểu luỹ tiến là bảng con: thiếu bậc thì THUE_LUYTIEN của mọi HĐLĐ ra 0.
        INSERT INTO PitBrackets (PitRegulationVersionId, [Order], LowerBound, UpperBound, Rate)
        SELECT @newPit, [Order], LowerBound, UpperBound, Rate
          FROM PitBrackets WHERE PitRegulationVersionId = @src;

        -- BR-15: mỗi version phải liên kết >= 1 LegalDocument.
        INSERT INTO ComplianceVersionLegalDocuments
            (ComplianceEntityType, ComplianceVersionId, LegalDocumentId, LinkedAt, LinkedByUserId)
        SELECT N'PitRegulation', @newPit, d.Id, SYSUTCDATETIME(), @admin
          FROM LegalDocuments d
         WHERE d.Code IN (N'LUAT-TNCN-109-2025', N'ND-253-2026');
    END
END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ComplianceVersionLegalDocuments
 WHERE ComplianceEntityType = N'PitRegulation'
   AND ComplianceVersionId IN (SELECT Id FROM PitRegulationVersions WHERE ValidFrom = '2026-07-01');

DELETE FROM PitBrackets
 WHERE PitRegulationVersionId IN (SELECT Id FROM PitRegulationVersions WHERE ValidFrom = '2026-07-01');

DELETE FROM PitRegulationVersions WHERE ValidFrom = '2026-07-01';

-- Trả cờ đang áp dụng về bản mới nhất còn lại, nếu không hệ thống rơi vào trạng thái không bản nào
-- hiệu lực (GetActiveAtAsync còn fallback theo ValidFrom nên vẫn chạy, nhưng /active trả 404).
UPDATE PitRegulationVersions
   SET IsActive = 1, UpdatedAt = SYSUTCDATETIME()
 WHERE Id = (SELECT TOP 1 Id FROM PitRegulationVersions
              WHERE IsArchived = 0 ORDER BY ValidFrom DESC, Id DESC)
   AND NOT EXISTS (SELECT 1 FROM PitRegulationVersions WHERE IsActive = 1 AND IsArchived = 0);");
        }
    }
}
