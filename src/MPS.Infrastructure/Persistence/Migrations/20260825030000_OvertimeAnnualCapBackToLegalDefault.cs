using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ⭐ OT-YEAR-CAP-2026-08 — hạ trần OT năm của các phiên bản luật CÒN NGUYÊN SEED từ 300h về 200h.
    ///
    /// <para>Đ.107.3 BLLĐ đặt trần chung 200 giờ/năm. 300 giờ chỉ dành cho danh mục ngành được liệt
    /// kê (dệt may, da giày, điện tử, chế biến nông–lâm–thuỷ sản, điện, viễn thông, lọc dầu, cấp
    /// thoát nước…) và doanh nghiệp còn phải thông báo bằng văn bản cho Sở LĐTBXH (NĐ 145/2020
    /// Đ.62). Seed để 300 nên <c>OvertimeCapChecker</c> — vốn tin tuyệt đối vào hằng số này — lặng
    /// lẽ cho vượt 200h ở mọi doanh nghiệp không thuộc diện.</para>
    ///
    /// <para><b>Chỉ đụng hàng CÒN NGUYÊN SEED</b>, nhận diện bằng đúng cặp
    /// (<c>OvertimeMaxHoursPerYear = 300</c>, <c>LegalReference</c> khớp chuỗi seed). Doanh nghiệp
    /// thuộc diện 300h mà đã tự khai thì <c>LegalReference</c> phải mang căn cứ riêng — chính là hồ
    /// sơ cho quyết định đó — nên không bị migration này chạm vào.</para>
    ///
    /// <para>Doanh nghiệp thuộc diện 300h nhưng chưa kịp khai: mở màn hình hằng số lao động, nâng
    /// lại trần và ghi căn cứ vào <c>LegalReference</c>. Chặn nhầm rồi mở ra rẻ hơn nhiều so với
    /// việc phát hiện đã dùng vượt trần sau một năm.</para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260825030000_OvertimeAnnualCapBackToLegalDefault")]
    public partial class OvertimeAnnualCapBackToLegalDefault : Migration
    {
        private const string SeededOldLawReference =
            "BLLĐ 2019 Điều 26, 98, 107, 113 + NĐ 145/2020 Điều 54 + TT 111/2013 Đ.3 (miễn thuế OT phần chênh lệch)";

        private const string SeededCurrentLawReference =
            "Luật TNCN 109/2025/QH15 + NĐ 253/2026/NĐ-CP — miễn thuế toàn bộ tiền lương làm đêm/OT từ 01/07/2026";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
UPDATE LaborLawConstantsVersions
   SET OvertimeMaxHoursPerYear = 200
 WHERE OvertimeMaxHoursPerYear = 300
   AND LegalReference IN (N'{SeededOldLawReference}', N'{SeededCurrentLawReference}');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
UPDATE LaborLawConstantsVersions
   SET OvertimeMaxHoursPerYear = 300
 WHERE OvertimeMaxHoursPerYear = 200
   AND LegalReference IN (N'{SeededOldLawReference}', N'{SeededCurrentLawReference}');");
        }
    }
}
