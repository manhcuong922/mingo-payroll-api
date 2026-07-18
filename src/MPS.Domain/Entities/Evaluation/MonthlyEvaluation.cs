using System.ComponentModel.DataAnnotations;
using MPS.Domain.Attributes;
using MPS.Domain.Enums.Evaluation;

namespace MPS.Domain.Entities.Evaluation;

/// <summary>
/// Bảng THÁNG chuẩn — 1 bảng / (NV, tháng) là chuẩn DUY NHẤT tính phụ cấp tháng đó (D11).
/// UNIQUE (EmployeeUserId, PeriodYear, PeriodMonth).
///
/// Source = <see cref="EvaluationSource.Aggregated"/> (công nhân): FinalScore = trung bình cộng
/// các bảng-theo-đơn đã Submit (D7); trace qua <see cref="SourceLinks"/>.
/// Source = <see cref="EvaluationSource.Direct"/> (tổ trưởng/văn phòng): Director chấm trực tiếp
/// vào <see cref="Scores"/> (tiêu chí snapshot theo role NV).
///
/// HR duyệt (Draft → Approved) + khóa; HR reopen được (Approved → Draft, giữ lịch sử). Xem plan §3.3.
/// </summary>
[Auditable]
public class MonthlyEvaluation
{
    public int Id { get; set; }

    public int EmployeeUserId { get; set; }
    public User? Employee { get; set; }

    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }

    public int SubjectRoleIdSnapshot { get; set; }
    public EvaluationSource Source { get; set; }

    // Thang 1–5 (decimal(4,2)) — KHÔNG xếp loại chữ (D12).
    public decimal? FinalScore { get; set; }

    public EvaluationStatus Status { get; set; } = EvaluationStatus.Draft;   // chỉ Draft | Approved

    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public int? ReopenedByUserId { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? ReopenReason { get; set; }

    // Direct: Director; Aggregated: HR/hệ thống (người bấm tổng hợp).
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Source = Aggregated → trace các bảng-theo-đơn đã gộp.
    public List<MonthlyEvaluationSourceLink> SourceLinks { get; set; } = new();

    // Source = Direct → tiêu chí snapshot + điểm Director nhập.
    public List<EvaluationScoreLine> Scores { get; set; } = new();
}
