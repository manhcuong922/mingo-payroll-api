using MPS.Application.DTOs.Evaluation;
using MPS.Application.Features.Evaluation.OrderEvaluation.Commands;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Xuất/nhập Excel cho bảng-theo-đơn: tổ trưởng tải mẫu (đã bao chứa rubric + hướng dẫn nghiệp vụ),
/// điền điểm offline rồi upload để chấm hàng loạt. Xem plan Phần 2.
/// </summary>
public interface IOrderEvaluationExcelService
{
    /// <summary>Sinh workbook mẫu (.xlsx) cho 1 bảng-theo-đơn: sheet Hướng dẫn + sheet Chấm điểm.</summary>
    byte[] BuildTemplate(OrderEvaluationDto evaluation);

    /// <summary>
    /// Đọc điểm từ file Excel người dùng upload (map theo cột ẩn subjectId/lineId). Chỉ trả các dòng
    /// đã điểm hợp lệ (1..5). Ném <c>DomainValidationException</c> nếu file sai định dạng / điểm ngoài 1..5.
    /// </summary>
    IReadOnlyList<ScoreSubjectInput> ParseScores(Stream xlsx);
}
