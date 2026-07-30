using MPS.Domain.Enums.Attendance;

namespace MPS.Application.DTOs.Attendance;

public sealed record FaceRegistrationDto(
    Guid Id,
    int EmployeeInformationId,
    string EmployeeFullName,
    string PhotoUrl,
    FaceRegistrationStatus Status,
    string? RejectReason,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewedByFullName);

public sealed record FaceEmbeddingDto(
    string EmployeeId,
    string EmployeeCode,
    string FullName,
    float[] Embedding);

public enum FaceCheckinType
{
    Checkin,
    Checkout,
    Cooldown,
    AlreadyComplete,
    Failed
}

public sealed record FaceCheckinResultDto(
    bool Success,
    FaceCheckinType CheckinType,
    string? AttendanceLogId,
    string Message,
    string? ShiftName,
    TimeOnly? CheckinTime,
    TimeOnly? CheckoutTime,
    int? CooldownSecondsRemaining);
