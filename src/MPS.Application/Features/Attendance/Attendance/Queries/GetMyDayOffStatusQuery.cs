using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

/// <summary>
/// Hôm nay (hoặc ngày chỉ định) user đang đăng nhập có nghỉ không.
/// <paramref name="Date"/> bỏ trống → lấy ngày hiện tại theo giờ Asia/Ho_Chi_Minh.
/// </summary>
public sealed record GetMyDayOffStatusQuery(DateOnly? Date) : IRequest<DayOffStatusDto>;
