using MPS.Domain.Constants;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Attendance.Common;

/// <summary>
/// Auto-routing đơn từ nhân sự (Leave, LateEarly, WFH) theo role của người tạo:
/// - Planning Manager tạo → force <see cref="OvertimeApproverRoute.Hr"/> (không có PM khác trên).
/// - HR tạo → force <see cref="OvertimeApproverRoute.Director"/> (HR không tự duyệt).
/// - Role khác → giữ giá trị FE gửi (bắt buộc ∈ Hr, DepartmentManager); không được truyền Director.
/// </summary>
public static class ApproverRouteResolver
{
    public static OvertimeApproverRoute Resolve(OvertimeApproverRoute? requested, int? creatorRoleId)
    {
        if (creatorRoleId == SystemRoles.DepartmentManagerId)
            return OvertimeApproverRoute.Hr;

        if (creatorRoleId == SystemRoles.HrId)
            return OvertimeApproverRoute.Director;

        if (requested is null)
            throw new DomainValidationException(
                "ApproverRoute là bắt buộc (chọn HR hoặc Planning Manager).");

        if (requested == OvertimeApproverRoute.Director)
            throw new DomainValidationException(
                "Chỉ HR mới được đẩy đơn cho Director duyệt.");

        if (requested != OvertimeApproverRoute.Hr
            && requested != OvertimeApproverRoute.DepartmentManager)
        {
            throw new DomainValidationException($"Route duyệt không hợp lệ: {requested}.");
        }

        return requested.Value;
    }
}
