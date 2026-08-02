using MediatR;

namespace MPS.Application.Features.Department.Commands;

/// <summary>
/// Gỡ một nhân viên khỏi xưởng (đặt <c>DepartmentId = null</c>).
///
/// <para><b>Vì sao phải là endpoint riêng</b>: <c>PUT /api/EmployeeInformation/{id}</c> dùng ngữ
/// nghĩa MERGE — <c>null</c> nghĩa là "không gửi, giữ nguyên giá trị cũ" (quy ước có chủ đích, để
/// HR sửa một trường mà không xoá trắng mã số thuế / số tài khoản / liên kết tài khoản). Hệ quả:
/// qua endpoint đó KHÔNG có cách nào diễn đạt "gỡ khỏi xưởng" — gửi <c>departmentId: null</c> trả
/// 204 nhưng xưởng vẫn nguyên, còn UI thì báo "Đã gỡ … khỏi xưởng" (thành công giả).</para>
///
/// <para>Không dùng sentinel (<c>departmentId = 0</c>) vì đó là nghĩa ngầm khó đọc và dễ vỡ; cũng
/// không đổi endpoint sửa hồ sơ sang optional-wrapper vì ngữ nghĩa merge đang được test
/// <c>EI-PUT-WIPE01</c> bảo vệ.</para>
/// </summary>
public sealed record DepartmentRemoveEmployeeCommand(
    int DepartmentId,
    int EmployeeInformationId) : IRequest<bool>;
