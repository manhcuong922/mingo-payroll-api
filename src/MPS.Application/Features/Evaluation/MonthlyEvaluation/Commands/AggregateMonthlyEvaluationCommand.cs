using MediatR;

namespace MPS.Application.Features.Evaluation.MonthlyEvaluation.Commands;

/// <summary>HR bấm "Tổng hợp tháng" — gộp các bảng-theo-đơn đã Submit thành bảng tháng (D7, D15).
/// Trả số bảng tháng được tạo/cập nhật. Xem plan §4.3.</summary>
public record AggregateMonthlyEvaluationCommand(int Year, int Month) : IRequest<int>;
