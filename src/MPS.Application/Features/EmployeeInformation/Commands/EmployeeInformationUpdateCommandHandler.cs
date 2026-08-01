using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Application.Common.Models.Notifications;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.EmployeeInformation.Commands;

/// <summary>
/// Cập nhật hồ sơ NV theo ngữ nghĩa MERGE (#46), không phải full-replace.
///
/// <para><b>Nghiệp vụ</b> (`CAC-CASE-XAY-RA` §3.1): *"Nhân sự chỉ sửa MỘT trường... mọi trường
/// không gửi lên đều giữ nguyên giá trị cũ — đặc biệt là mã số thuế, số tài khoản ngân hàng và
/// liên kết tài khoản đăng nhập, vì mất các trường này sẽ khiến nhân viên rớt khỏi điều kiện tính
/// lương của cả công ty."*</para>
///
/// <para><b>Trước</b>: endpoint là full-replace — trường không gửi bị ghi <c>null</c>, kể cả
/// <c>UserId</c> (gỡ liên kết tài khoản), <c>TaxNo</c>, <c>BankNumber</c>. Sửa 1 field mà quên gửi
/// lại phần còn lại là xoá trắng hồ sơ chi lương ⇒ NV rớt readiness ⇒ **cả công ty không tạo được
/// kỳ lương**, không một cảnh báo.</para>
///
/// <para><b>Quy ước phân biệt "không gửi" và "xoá có chủ đích"</b> cho các trường tuỳ chọn:
/// <list type="bullet">
///   <item><c>null</c> = KHÔNG gửi ⇒ giữ nguyên giá trị cũ;</item>
///   <item>chuỗi rỗng/space = XOÁ có chủ đích ⇒ ghi <c>null</c>;</item>
///   <item>có giá trị = ghi giá trị (đã trim).</item>
/// </list>
/// FE gửi chuỗi rỗng cho ô người dùng xoá, nên vẫn xoá được field khi CỐ Ý.</para>
///
/// <para><c>UserId</c> KHÔNG bao giờ đổi qua endpoint này — nó là liên kết tài khoản do luồng cấp
/// tài khoản (promote) quản, không thuộc phần HR sửa hồ sơ ⇒ luôn giữ giá trị cũ.</para>
/// </summary>
public class EmployeeInformationUpdateCommandHandler : IRequestHandler<EmployeeInformationUpdateCommand, bool>
{
    private readonly IEmployeeInformationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;

    public EmployeeInformationUpdateCommandHandler(
        IEmployeeInformationRepository repository,
        ICurrentUserService currentUserService,
        IMediator mediator)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _mediator = mediator;
    }

    public async Task<bool> Handle(EmployeeInformationUpdateCommand request, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (!_currentUserService.HasPermission(Permissions.EmployeeInformationUpdateAll))
        {
            if (existing.UserId != _currentUserService.UserId)
                throw new ForbiddenAccessException("You can only update your own employee information.");
        }

        // #47 — vai trò quản trị cấp công ty (HR/Kế toán/Giám đốc…) KHÔNG trực thuộc xưởng. Seed lỡ
        // gán xưởng cho 3 hồ sơ này ⇒ trước đây validator chặn, HR không sửa nổi field nào của họ.
        // Theo §3.1: "hệ thống để trống thông tin xưởng cho các hồ sơ này thay vì chặn" ⇒ tự chuẩn
        // hoá DepartmentId về null cho role quản trị, thay vì báo lỗi. (Nhánh CREATE vẫn chặn loud
        // khi cố GÁN xưởng cho role quản trị — xem EI-DEPT-ROLE01.)
        var mergedDepartmentId = Merge(request.DepartmentId, existing.DepartmentId);
        if (mergedDepartmentId is not null && existing.UserId is int uid)
        {
            var roleId = await _repository.GetUserRoleIdAsync(uid, cancellationToken);
            if (roleId is int r && !SystemRoles.DepartmentMemberRoleIds.Contains(r))
                mergedDepartmentId = null;
        }

        var entity = new Domain.Entities.EmployeeInformation
        {
            Id = request.Id,
            // UserId: liên kết tài khoản — không đổi qua endpoint sửa hồ sơ.
            UserId = existing.UserId,
            // Trường bắt buộc (validator đã đảm bảo có mặt & hợp lệ) — dùng giá trị gửi lên.
            FullName = request.FullName.Trim(),
            Dob = request.Dob,
            Gender = request.Gender,
            CitizenId = request.CitizenId.Trim(),
            PermanentAddress = request.PermanentAddress.Trim(),
            PositionId = request.PositionId,
            // #40 — `DependentsNo` là số DẪN XUẤT từ các bản ghi người phụ thuộc đã duyệt (luồng
            // duyệt NPT tự đồng bộ), KHÔNG cho khai tay qua sửa hồ sơ. Trước đây nhận thẳng số gửi
            // lên nên `PUT DependentsNo = 99` (0 bản ghi, 0 chứng từ) lưu được ⇒ nếu NV chưa có bản
            // ghi thì đây là suất giảm trừ khống. Theo §3.2: "không nhận số nhập tay không có căn cứ".
            DependentsNo = existing.DependentsNo,
            // Trường tuỳ chọn: null = giữ nguyên, "" = xoá, có giá trị = ghi.
            CitizenIssuedDate = request.CitizenIssuedDate ?? existing.CitizenIssuedDate,
            CitizenIssuedPlace = Merge(request.CitizenIssuedPlace, existing.CitizenIssuedPlace),
            ContactAddress = Merge(request.ContactAddress, existing.ContactAddress),
            Email = Merge(request.Email, existing.Email),
            PhoneNo = Merge(request.PhoneNo, existing.PhoneNo),
            EducationLevel = Merge(request.EducationLevel, existing.EducationLevel),
            DepartmentId = mergedDepartmentId,
            TaxNo = Merge(request.TaxNo, existing.TaxNo),
            SocialNo = Merge(request.SocialNo, existing.SocialNo),
            BankNumber = Merge(request.BankNumber, existing.BankNumber),
            GuardianFullName = Merge(request.GuardianFullName, existing.GuardianFullName),
            GuardianRelationship = Merge(request.GuardianRelationship, existing.GuardianRelationship),
            GuardianPhoneNo = Merge(request.GuardianPhoneNo, existing.GuardianPhoneNo),
            GuardianAddress = Merge(request.GuardianAddress, existing.GuardianAddress),
        };

        var updated = await _repository.UpdateAsync(entity, cancellationToken);

        // Publish notification khi dept thực sự đổi — handler AutoApplySchedule tự động
        // gỡ ca dept cũ và apply ca dept mới. Không publish nếu update thất bại hoặc dept không đổi.
        if (updated && existing.DepartmentId != request.DepartmentId)
        {
            await _mediator.Publish(
                new EmployeeDepartmentChangedNotification(
                    EmployeeInformationId: request.Id,
                    OldDepartmentId: existing.DepartmentId,
                    NewDepartmentId: request.DepartmentId,
                    ChangedByUserId: _currentUserService.UserId ?? 0,
                    ChangedAtUtc: DateTime.UtcNow),
                cancellationToken);
        }

        // Phase 6 §N12 — cảnh báo sửa field nhạy cảm (CCCD/STK/TaxNo/SocialNo).
        // DependentsNo không kiểm ở đây vì không cho khai tay qua endpoint này (#40).
        if (updated)
        {
            var changed = new List<string>();
            if (!string.Equals(existing.CitizenId, entity.CitizenId, StringComparison.Ordinal))
                changed.Add("CCCD");
            if (!string.Equals(existing.BankNumber, entity.BankNumber, StringComparison.Ordinal))
                changed.Add("Số tài khoản");
            if (!string.Equals(existing.TaxNo, entity.TaxNo, StringComparison.Ordinal))
                changed.Add("Mã số thuế");
            if (!string.Equals(existing.SocialNo, entity.SocialNo, StringComparison.Ordinal))
                changed.Add("Số sổ BHXH");

            if (changed.Count > 0)
            {
                await _mediator.Publish(new EmployeeSensitiveEditedNotification(
                    EmployeeInformationId: request.Id,
                    EmployeeUserId: existing.UserId,
                    ChangedByUserId: _currentUserService.UserId ?? 0,
                    ChangedFields: changed), cancellationToken);
            }
        }

        return updated;
    }

    /// <summary>null = giữ nguyên; rỗng/space = xoá (null); ngược lại = trim rồi ghi.</summary>
    private static string? Merge(string? incoming, string? existing) =>
        incoming is null ? existing
        : string.IsNullOrWhiteSpace(incoming) ? null
        : incoming.Trim();

    /// <summary>null = giữ nguyên; ngược lại = giá trị mới (kể cả để đổi xưởng).</summary>
    private static int? Merge(int? incoming, int? existing) => incoming ?? existing;
}
