using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Backfill số liệu compliance mới cho DB ĐÃ CÓ DỮ LIỆU (production) — nơi <c>DatabaseSeeder</c>
    /// KHÔNG bao giờ chạm tới: mọi seeder compliance mở đầu bằng
    /// <c>if (await _context.&lt;Bảng&gt;.AnyAsync()) return;</c>, mà prod đặt
    /// <c>SEED_RESET_ON_STARTUP=false</c> nên bảng luôn có sẵn hàng ⇒ sửa seed là vô tác dụng.
    ///
    /// <para>Mỗi khối tự chặn hai đầu:</para>
    /// <list type="bullet">
    ///   <item><b>Chỉ chạy khi bảng ĐÃ có dữ liệu.</b> Trên DB sạch (local/CI) migration chạy TRƯỚC
    ///     seeder nên bảng còn rỗng ⇒ bỏ qua để seeder dựng trọn bộ. Không có chốt này thì migration
    ///     chèn 1 hàng, seeder thấy "đã có dữ liệu" rồi skip sạch phần còn lại (mất BHXH/PIT/FUEL/PHONE).</item>
    ///   <item><b>Chỉ chạy khi chưa có bản 01/07/2026</b> ⇒ idempotent.</item>
    /// </list>
    ///
    /// Nội dung: NĐ 253/2026/NĐ-CP + LaborLawConstants miễn thuế TOÀN BỘ tiền tăng ca + ngưỡng phụ cấp
    /// ăn ca 1.200.000 thay 730.000. Xem <c>LaborLawConstantsVersion.OvertimeIncomeFullyTaxExempt</c>.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260810160000_BackfillOvertimeTaxExemptionCompliance")]
    public partial class BackfillOvertimeTaxExemptionCompliance : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===== 1. Văn bản NĐ 253/2026/NĐ-CP =====
            migrationBuilder.Sql(@"
DECLARE @admin INT = (SELECT TOP 1 Id FROM Users WHERE RoleId = 1 ORDER BY Id);

IF @admin IS NOT NULL
   AND EXISTS (SELECT 1 FROM LegalDocuments)
   AND NOT EXISTS (SELECT 1 FROM LegalDocuments WHERE Code = N'ND-253-2026')
BEGIN
    INSERT INTO LegalDocuments
        (Code, DocumentType, DocumentNumber, Title, IssuedBy, IssuedDate, EffectiveDate, IsActive, CreatedAt, CreatedByUserId)
    VALUES
        (N'ND-253-2026', N'NghiDinh', N'253/2026/NĐ-CP',
         N'Ngưỡng phụ cấp ăn ca/ăn trưa miễn thuế 1,200,000 từ 1/7/2026',
         N'Chính phủ', '2026-06-30', '2026-07-01', 1, SYSUTCDATETIME(), @admin);
END;");

            // ===== 2. Hằng số luật lao động — miễn thuế toàn bộ tiền tăng ca từ 01/07/2026 =====
            // Sao chép nguyên hằng số của bản đang áp dụng, chỉ đổi cờ miễn thuế + ValidFrom, để không
            // vô tình ghi đè hệ số OT / khung giờ đêm mà kế toán đã chỉnh tay trên prod.
            migrationBuilder.Sql(@"
DECLARE @admin INT = (SELECT TOP 1 Id FROM Users WHERE RoleId = 1 ORDER BY Id);

IF @admin IS NOT NULL
   AND EXISTS (SELECT 1 FROM LaborLawConstantsVersions)
   AND NOT EXISTS (SELECT 1 FROM LaborLawConstantsVersions WHERE ValidFrom = '2026-07-01')
BEGIN
    DECLARE @src INT = (
        SELECT TOP 1 Id FROM LaborLawConstantsVersions
        WHERE IsArchived = 0 AND ValidFrom <= '2026-07-01'
        ORDER BY IsActive DESC, ValidFrom DESC);

    IF @src IS NOT NULL
    BEGIN
        -- PHẢI hạ cờ bản cũ TRƯỚC khi chèn: IX_LaborLawConstantsVersions_IsActive là unique index lọc
        -- (IsActive=1 AND IsArchived=0) ⇒ chỉ được tồn tại ĐÚNG 1 hàng active. Chèn trước rồi mới hạ
        -- cờ sẽ vi phạm index ⇒ migration fail ⇒ API không khởi động được.
        UPDATE LaborLawConstantsVersions
           SET IsActive = 0, UpdatedAt = SYSUTCDATETIME()
         WHERE IsActive = 1 AND IsArchived = 0;

        INSERT INTO LaborLawConstantsVersions
            (ValidFrom, IsActive, IsArchived, ProbationSalaryMinPercent, OvertimeNightExtraPercent,
             NightWorkSurchargePercent, OvertimeNormalUpliftPercent, NightStartTime, NightEndTime,
             StandardWorkingHoursPerDay, MaxRegularHoursPerWeek, StandardWorkingDaysPerMonth,
             AnnualLeaveMinDays, WorkRateNormalFloor, WorkRateWeeklyRestFloor, WorkRateHolidayFloor,
             OvertimeMaxHoursPerDay, OvertimeMaxHoursPerMonth, OvertimeMaxHoursPerYear,
             OvertimeIncomeFullyTaxExempt, LegalReference, CreatedAt, CreatedByUserId)
        SELECT
             '2026-07-01', 1, 0, ProbationSalaryMinPercent, OvertimeNightExtraPercent,
             NightWorkSurchargePercent, OvertimeNormalUpliftPercent, NightStartTime, NightEndTime,
             StandardWorkingHoursPerDay, MaxRegularHoursPerWeek, StandardWorkingDaysPerMonth,
             AnnualLeaveMinDays, WorkRateNormalFloor, WorkRateWeeklyRestFloor, WorkRateHolidayFloor,
             OvertimeMaxHoursPerDay, OvertimeMaxHoursPerMonth, OvertimeMaxHoursPerYear,
             1,
             N'Luật TNCN 109/2025/QH15 + NĐ 253/2026/NĐ-CP — miễn thuế toàn bộ tiền lương làm đêm/OT từ 01/07/2026',
             SYSUTCDATETIME(), CreatedByUserId
        FROM LaborLawConstantsVersions WHERE Id = @src;

        DECLARE @newLabor INT = CAST(SCOPE_IDENTITY() AS INT);

        -- BR-15: mỗi version phải liên kết >= 1 LegalDocument.
        INSERT INTO ComplianceVersionLegalDocuments
            (ComplianceEntityType, ComplianceVersionId, LegalDocumentId, LinkedAt, LinkedByUserId)
        SELECT N'LaborLawConstants', @newLabor, d.Id, SYSUTCDATETIME(), @admin
          FROM LegalDocuments d
         WHERE d.Code IN (N'LUAT-TNCN-109-2025', N'ND-253-2026');
    END
END;");

            // ===== 3. Ngưỡng phụ cấp ăn ca 730.000 -> 1.200.000 =====
            // AllowanceExemptThresholdVersion KHÔNG được ComplianceActivationJob xử lý ("HR bắt buộc
            // dùng applyImmediately=true") nên phải bật cờ thẳng tại đây.
            migrationBuilder.Sql(@"
DECLARE @admin INT = (SELECT TOP 1 Id FROM Users WHERE RoleId = 1 ORDER BY Id);
DECLARE @meal INT = (SELECT TOP 1 Id FROM AllowanceTypes WHERE Code = 'MEAL');

IF @admin IS NOT NULL AND @meal IS NOT NULL
   AND EXISTS (SELECT 1 FROM AllowanceExemptThresholdVersions)
   AND NOT EXISTS (SELECT 1 FROM AllowanceExemptThresholdVersions
                    WHERE AllowanceTypeId = @meal AND ValidFrom = '2026-07-01')
BEGIN
    -- Hạ cờ TRƯỚC khi chèn: IX_AllowanceExemptThresholdVersions_AllowanceTypeId_IsActive là unique
    -- index lọc (IsActive=1 AND IsArchived=0) ⇒ mỗi loại phụ cấp chỉ được ĐÚNG 1 hàng active.
    UPDATE AllowanceExemptThresholdVersions
       SET IsActive = 0, UpdatedAt = SYSUTCDATETIME()
     WHERE AllowanceTypeId = @meal AND IsActive = 1 AND IsArchived = 0;

    INSERT INTO AllowanceExemptThresholdVersions
        (ValidFrom, IsActive, IsArchived, AllowanceTypeId, ThresholdMode, MonthlyExemptAmount,
         AppliesToTax, AppliesToSocial, LegalReference, CreatedAt, CreatedByUserId)
    VALUES
        ('2026-07-01', 1, 0, @meal, N'FixedAmount', 1200000,
         1, 1, N'NĐ 253/2026/NĐ-CP — ngưỡng mới 1,200,000 từ 01/07/2026',
         SYSUTCDATETIME(), @admin);

    DECLARE @newMeal INT = CAST(SCOPE_IDENTITY() AS INT);

    INSERT INTO ComplianceVersionLegalDocuments
        (ComplianceEntityType, ComplianceVersionId, LegalDocumentId, LinkedAt, LinkedByUserId)
    SELECT N'AllowanceExemptThreshold', @newMeal, d.Id, SYSUTCDATETIME(), @admin
      FROM LegalDocuments d WHERE d.Code = N'ND-253-2026';
END;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ComplianceVersionLegalDocuments
 WHERE ComplianceEntityType = N'LaborLawConstants'
   AND ComplianceVersionId IN (SELECT Id FROM LaborLawConstantsVersions WHERE ValidFrom = '2026-07-01');
DELETE FROM LaborLawConstantsVersions WHERE ValidFrom = '2026-07-01';

DELETE FROM ComplianceVersionLegalDocuments
 WHERE ComplianceEntityType = N'AllowanceExemptThreshold'
   AND ComplianceVersionId IN (
       SELECT v.Id FROM AllowanceExemptThresholdVersions v
        JOIN AllowanceTypes t ON t.Id = v.AllowanceTypeId
       WHERE t.Code = 'MEAL' AND v.ValidFrom = '2026-07-01');
DELETE v FROM AllowanceExemptThresholdVersions v
  JOIN AllowanceTypes t ON t.Id = v.AllowanceTypeId
 WHERE t.Code = 'MEAL' AND v.ValidFrom = '2026-07-01';

DELETE FROM ComplianceVersionLegalDocuments
 WHERE LegalDocumentId IN (SELECT Id FROM LegalDocuments WHERE Code = N'ND-253-2026');
DELETE FROM LegalDocuments WHERE Code = N'ND-253-2026';");
        }
    }
}
