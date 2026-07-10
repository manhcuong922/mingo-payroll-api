using MPS.Domain.Constants;

namespace MPS.Infrastructure.Persistence;

/// <summary>1 tiêu chí con (PerformanceMetric) trong 1 nhóm — kèm các role được chấm bằng tiêu chí này.</summary>
public sealed record MetricSeed(string Name, decimal Weight, string Description, int[] TargetRoleIds);

/// <summary>1 nhóm tiêu chí (TypePerformanceMetric) + trọng số nhóm + các tiêu chí con.</summary>
public sealed record CriteriaGroupSeed(string TypeName, decimal TypeWeight, MetricSeed[] Metrics);

/// <summary>
/// Bộ tiêu chí đánh giá mặc định cho module chấm điểm tháng — seed vào catalog
/// (<c>TypePerformanceMetric</c> + <c>PerformanceMetric</c>) để module hoạt động ngay:
/// không có tiêu chí cho role 7 thì bảng-theo-đơn không bao giờ sinh (E3).
///
/// Ràng buộc catalog: tổng <c>Weight</c> tiêu chí con trong 1 nhóm ≤ 100. Mỗi nhóm ở đây gắn
/// duy nhất 1 "profile role" nên tổng đúng bằng 100 (test <c>EvaluationCriteriaSeedDataTests</c> canh).
/// Admin có thể sửa/thêm/xóa qua API sau khi seed.
/// </summary>
public static class EvaluationCriteriaSeedData
{
    // Profile A — Công nhân (role 7): auto sinh bảng-theo-đơn khi đơn Completed.
    private static readonly int[] Worker = { SystemRoles.EmployeeId };

    // Profile B — Quản lý sản xuất (Tổ trưởng 6 + Planning Manager 5): Director chấm trực tiếp.
    private static readonly int[] ProductionLead = { SystemRoles.ProductionTeamLeaderId, SystemRoles.PlanningManagerId };

    // Profile C — Khối văn phòng (HR 2 + Kế toán 3): Director chấm trực tiếp.
    private static readonly int[] Office = { SystemRoles.HrId, SystemRoles.AccountantId };

    public static IReadOnlyList<CriteriaGroupSeed> Groups { get; } = new[]
    {
        // ===== Công nhân (role 7) =====
        new CriteriaGroupSeed("Năng suất (Công nhân)", 40, new[]
        {
            new MetricSeed("Hoàn thành khối lượng được giao", 60, "Mức độ hoàn thành sản lượng/khối lượng công việc được giao trong tháng.", Worker),
            new MetricSeed("Đảm bảo tiến độ công đoạn", 40, "Hoàn thành công đoạn đúng hạn, không gây ùn tắc dây chuyền.", Worker),
        }),
        new CriteriaGroupSeed("Chất lượng (Công nhân)", 35, new[]
        {
            new MetricSeed("Tỷ lệ sản phẩm đạt, ít lỗi", 60, "Chất lượng sản phẩm làm ra, tỷ lệ lỗi/tái chế thấp.", Worker),
            new MetricSeed("Tuân thủ quy trình kỹ thuật & an toàn", 40, "Làm đúng quy trình kỹ thuật, đảm bảo an toàn lao động.", Worker),
        }),
        new CriteriaGroupSeed("Ý thức kỷ luật (Công nhân)", 25, new[]
        {
            new MetricSeed("Chấp hành nội quy, giờ giấc", 50, "Đi làm đầy đủ, đúng giờ, chấp hành nội quy xưởng.", Worker),
            new MetricSeed("Tinh thần hợp tác trong tổ", 50, "Phối hợp, hỗ trợ đồng nghiệp, tinh thần làm việc nhóm.", Worker),
        }),

        // ===== Quản lý sản xuất — Tổ trưởng (6) + Planning Manager (5) =====
        new CriteriaGroupSeed("Kết quả sản xuất (Quản lý SX)", 45, new[]
        {
            new MetricSeed("Hoàn thành kế hoạch/đơn được giao", 50, "Tổ/xưởng hoàn thành kế hoạch sản xuất, đơn hàng đúng tiến độ.", ProductionLead),
            new MetricSeed("Chất lượng đầu ra của tổ/xưởng", 50, "Chất lượng thành phẩm của tổ/xưởng phụ trách.", ProductionLead),
        }),
        new CriteriaGroupSeed("Năng lực điều hành (Quản lý SX)", 35, new[]
        {
            new MetricSeed("Phân công, giám sát, đào tạo nhân sự", 50, "Phân công hợp lý, giám sát và kèm cặp công nhân.", ProductionLead),
            new MetricSeed("Xử lý sự cố & cải tiến", 50, "Chủ động xử lý sự cố sản xuất và đề xuất cải tiến.", ProductionLead),
        }),
        new CriteriaGroupSeed("Kỷ luật & Báo cáo (Quản lý SX)", 20, new[]
        {
            new MetricSeed("Chấp hành nội quy, an toàn lao động", 50, "Gương mẫu chấp hành nội quy và đảm bảo an toàn cho tổ.", ProductionLead),
            new MetricSeed("Báo cáo tiến độ đầy đủ, đúng hạn", 50, "Cập nhật tiến độ, báo cáo đầy đủ và kịp thời.", ProductionLead),
        }),

        // ===== Khối văn phòng — HR (2) + Kế toán (3) =====
        new CriteriaGroupSeed("Kết quả công việc (Văn phòng)", 45, new[]
        {
            new MetricSeed("Hoàn thành nhiệm vụ chuyên môn", 60, "Hoàn thành khối lượng và nhiệm vụ chuyên môn được giao.", Office),
            new MetricSeed("Độ chính xác & chất lượng", 40, "Chất lượng, độ chính xác của kết quả công việc.", Office),
        }),
        new CriteriaGroupSeed("Năng lực & Phối hợp (Văn phòng)", 35, new[]
        {
            new MetricSeed("Chủ động, giải quyết vấn đề", 50, "Chủ động trong công việc và xử lý vấn đề phát sinh.", Office),
            new MetricSeed("Phối hợp liên phòng ban", 50, "Phối hợp hiệu quả với các bộ phận khác.", Office),
        }),
        new CriteriaGroupSeed("Kỷ luật (Văn phòng)", 20, new[]
        {
            new MetricSeed("Chấp hành nội quy, giờ giấc", 50, "Đi làm đúng giờ, chấp hành nội quy công ty.", Office),
            new MetricSeed("Tinh thần trách nhiệm", 50, "Trách nhiệm với công việc và kết quả chung.", Office),
        }),
    };
}
