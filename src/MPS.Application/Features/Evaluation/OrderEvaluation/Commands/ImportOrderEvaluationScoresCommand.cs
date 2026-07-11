using MediatR;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Commands;

/// <summary>
/// Tổ trưởng upload Excel (đã điền điểm) để chấm hàng loạt 1 bảng-theo-đơn (Draft).
/// Dùng rowVersion hiện tại của server (luồng offline). Trả số dòng điểm đã áp. Xem plan Phần 2.
/// </summary>
public record ImportOrderEvaluationScoresCommand(int Id, byte[] FileContent) : IRequest<int>;
