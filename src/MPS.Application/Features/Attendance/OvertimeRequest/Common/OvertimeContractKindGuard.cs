using MPS.Domain.Constants;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Common;

/// <summary>
/// X5 — chặn đăng ký tăng ca trên HỢP ĐỒNG DÂN SỰ (khoán việc, dịch vụ, cộng tác viên).
///
/// <para><b>Vì sao.</b> "Làm thêm giờ" là khái niệm của Bộ luật Lao động: có thời giờ làm việc bình
/// thường để mà làm THÊM, và có hệ số 150%/200%/300% (Đ.98). Quan hệ dân sự không có thời giờ làm
/// việc — hai bên thoả thuận KHỐI LƯỢNG và tiền công trọn gói, làm nhanh hay chậm là việc của bên
/// nhận khoán. Không handler nào trong <c>Features/Attendance/OvertimeRequest/</c> từng đọc
/// <c>ContractKind</c>, nên người ký hợp đồng khoán việc vẫn đăng ký được, quản lý vẫn duyệt được,
/// và tiền vẫn chảy vào cột "Tăng ca" theo hệ số của BLLĐ.</para>
///
/// <para><b>Rủi ro là pháp lý, không phải rủi ro số.</b> Số tiền đúng theo công thức, nhưng chứng từ
/// do chính công ty phát hành lại là bằng chứng thuận lợi cho bên kia nếu họ khởi kiện đòi công nhận
/// quan hệ lao động (Đ.13.1 BLLĐ: gọi tên gì không quan trọng, nội dung mới quyết định). Nó cũng mâu
/// thuẫn với chính quyết định thiết kế của dự án — HĐ khoán việc đã có cơ chế riêng là "Hỗ trợ theo
/// tiến độ" theo mốc tổng giờ, và <c>PayrollColumnCodes.HoTroTienDo</c> ghi rõ chứng từ của hợp đồng
/// dân sự không được mang ngôn ngữ của quan hệ lao động.</para>
///
/// <para>Phân loại theo <see cref="ContractKindMetadata"/> chứ không liệt kê tay ba loại hợp đồng:
/// thêm loại HĐ dân sự mới sau này là guard tự phủ, không phải nhớ sửa ở đây.</para>
///
/// <para>Thuần số học (không DbContext) để test độc lập.</para>
/// </summary>
public static class OvertimeContractKindGuard
{
    /// <param name="activeKinds">
    /// Loại của các hợp đồng còn hiệu lực của nhân viên. Danh sách RỖNG ⇒ BỎ QUA, không chặn: người
    /// gọi đã kiểm "còn hợp đồng hiệu lực" ngay trước đó (<c>CONTRACT_NOT_ACTIVE</c>), nên rỗng ở
    /// đây chỉ có thể là "không tra được", và guard này không phải chỗ báo lỗi đó.
    /// </param>
    public static void EnsureLaborRelationship(IReadOnlyCollection<ContractKind>? activeKinds)
    {
        if (activeKinds is null || activeKinds.Count == 0) return;

        // Còn BẤT KỲ hợp đồng lao động nào đang hiệu lực thì tăng ca là hợp lệ — người vừa có HĐLĐ
        // vừa nhận thêm việc khoán không bị chặn oan.
        if (activeKinds.Any(k => ContractKindMetadata.Get(k).LegalFramework == ContractLegalFramework.LaborCode))
            return;

        var ten = string.Join(", ", activeKinds
            .Distinct()
            .Select(k => ContractKindMetadata.Get(k).DisplayNameVi));

        throw new DomainValidationException(
            $"OT_CIVIL_CONTRACT: {ten} là quan hệ dân sự — không có khái niệm làm thêm giờ theo Bộ luật "
            + "Lao động. Khối lượng và tiền công đã thoả thuận trọn gói. Khoản trả thêm theo số giờ đã "
            + "làm được ghi nhận qua \"Hỗ trợ theo tiến độ\", không phải qua đơn tăng ca.");
    }
}
