namespace MPS.Application.Common.Payroll;

/// <summary>
/// Các lý do "chưa đủ liệu" do <c>PayrollReadinessChecker</c> phát ra.
///
/// <para>Trước đây chúng là chuỗi rời nằm trong checker. Tách ra hằng số vì màn hình TẠM TÍNH cần
/// phân biệt <b>một</b> lý do trong số đó với phần còn lại: thiếu hợp đồng đã ký là KHÔNG tính được
/// (không có mức lương nào để chia), còn thiếu đánh giá tháng / còn đơn OT chờ duyệt thì vẫn tạm
/// tính được với dữ liệu hiện có. So chuỗi bằng literal chép tay ở hai nơi là kiểu bug sửa một chỗ
/// quên chỗ kia — đổi câu chữ ở checker là bộ lọc tạm tính im lặng hỏng.</para>
///
/// <para>Chuỗi ở đây hiển thị thẳng cho người dùng, nên sửa câu chữ là đổi cả thông điệp trên UI.</para>
/// </summary>
public static class PayrollReadinessBlockers
{
    public const string NoSignedContract = "Chưa có hợp đồng đã ký hiệu lực trong kỳ";
    public const string NoAttendance = "Chưa có dữ liệu chấm công trong kỳ";
    public const string PendingOvertime = "Còn đơn tăng ca chưa duyệt xong";
    public const string NoApprovedEvaluation = "Chưa có bảng đánh giá tháng được duyệt";
    public const string IncompletePaymentProfile = "Hồ sơ chi lương thiếu (số TK / mã số thuế)";
}
