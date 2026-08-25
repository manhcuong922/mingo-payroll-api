using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MPS.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyRestDayConfiguration : Migration
    {
        private const string StaleReason =
            "Cách tính công/lương đã đổi: công nay cộng GIỜ cả kỳ rồi mới chia giờ chuẩn/ngày và làm " +
            "tròn lên bội số 0,5 (thay vì làm tròn từng ngày); ngày nghỉ hằng tuần lấy từ cấu hình " +
            "chấm công thay vì suy từ bảng phân ca (ca rơi vào T7/CN được trả đúng 200% — Đ.98.1b); " +
            "phần đi muộn/về sớm nằm trong grace không còn bị trừ công; đi làm ngày lễ được 400% " +
            "thay vì 300% (Đ.98.1c + Đ.112.1). Bấm Tính lại để cập nhật số.";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeeklyRestDayMask",
                table: "AttendanceSettings",
                type: "int",
                nullable: true);

            // Cột để NULL = chưa cấu hình ⇒ WeeklyRestDayPolicy.DefaultMask (T7 + CN). Cố ý KHÔNG
            // backfill một giá trị cứng: null còn phân biệt được "chưa ai khai" với "đã khai đúng
            // T7+CN", nên đổi mặc định sau này không phải đoán xem hàng nào là do migration đặt.

            // Engine đổi cách tính tiền theo BỐN hướng: (1) công cộng theo GIỜ cả kỳ rồi mới làm
            // tròn lên bội số 0,5, thay vì làm tròn 2 chữ số ở từng ngày; (2) ngày T7/CN có xếp ca
            // trước đây bị coi là ngày thường (100%), nay là ngày nghỉ hằng tuần (200%); (3) grace
            // đi muộn/về sớm trước chỉ đổi nhãn Present, nay được tha luôn phần công tương ứng;
            // (4) đi làm ngày lễ nay được phụ trội nguyên 300% (tổng 400%) thay vì phần chênh 200%.
            // Kỳ Draft đang giữ số CŨ cho tới khi ai đó
            // bấm "Tính lại" — chốt lúc này là chốt bằng số cũ mà không ai biết.
            // Chỉ đụng kỳ Draft (Status = 1); kỳ Finalized/Locked đã đóng băng theo snapshot.
            migrationBuilder.Sql($@"
UPDATE PayrollPeriods
   SET IsStale = 1,
       StaleReason = N'{StaleReason}',
       StaleMarkedAt = SYSUTCDATETIME(),
       UpdatedAt = SYSUTCDATETIME()
 WHERE Status = 1 AND IsStale = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WeeklyRestDayMask",
                table: "AttendanceSettings");

            // Cố ý KHÔNG hạ cờ stale: không có nguồn nào để biết kỳ nào vốn đã stale từ trước vì lý
            // do khác (sửa chấm công, đổi hợp đồng...). Hạ hết là xoá mất cảnh báo thật của kế toán.
        }
    }
}
