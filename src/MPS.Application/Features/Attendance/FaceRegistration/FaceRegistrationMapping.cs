using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.FaceRegistration;

/// <summary>
/// Map entity → DTO dùng chung cho cả 5 handler (Submit/Approve/Reject/GetAll/GetMy).
/// SubmittedAt/ReviewedAt lưu UTC (_clock.UtcNow) nhưng EF đọc từ DB trả về
/// Kind=Unspecified → serializer bỏ hậu tố 'Z' → FE parse nhầm thành giờ local
/// (hiển thị lệch −7h so với giờ Việt Nam). Ép Kind=Utc để JSON luôn có 'Z'.
/// </summary>
internal static class FaceRegistrationMapping
{
    public static FaceRegistrationDto ToDto(
        this Domain.Entities.Attendance.FaceRegistration entity,
        string photoUrl) =>
        new(
            entity.Id,
            entity.EmployeeInformationId,
            entity.EmployeeInformation?.FullName ?? string.Empty,
            photoUrl,
            entity.Status,
            entity.RejectReason,
            DateTime.SpecifyKind(entity.SubmittedAt, DateTimeKind.Utc),
            entity.ReviewedAt is null ? null : DateTime.SpecifyKind(entity.ReviewedAt.Value, DateTimeKind.Utc),
            entity.ReviewedByUser?.FullName);
}
