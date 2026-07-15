using MediatR;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

/// <summary>Director tạo bảng tháng Direct (Loại B) cho tổ trưởng/văn phòng — snapshot tiêu chí theo
/// role NV, Status = Draft. Xem plan §4.4, §13.9.</summary>
public record CreateDirectMonthlyEvaluationCommand(int EmployeeUserId, int Year, int Month) : IRequest<int>;
