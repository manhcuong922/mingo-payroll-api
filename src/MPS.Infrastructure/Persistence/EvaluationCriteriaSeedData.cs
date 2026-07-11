namespace MPS.Infrastructure.Persistence;

/// <summary>1 tiêu chí con (PerformanceMetric) trong 1 nhóm.</summary>
public sealed record MetricSeed(string Name, decimal Weight, string Description);

/// <summary>1 nhóm tiêu chí (TypePerformanceMetric) + trọng số nhóm + các tiêu chí con.</summary>
public sealed record CriteriaGroupSeed(string TypeName, decimal TypeWeight, MetricSeed[] Metrics);

/// <summary>
/// Bộ tiêu chí đánh giá mặc định — RUBRIC CHUNG toàn công ty (dùng cho mọi vai trò bị chấm).
/// Seed vào catalog (<c>TypePerformanceMetric</c> + <c>PerformanceMetric</c>) để module chấm điểm
/// chạy được ngay: không có tiêu chí thì bảng-theo-đơn không bao giờ sinh (E3).
///
/// Ràng buộc bắt buộc để tính điểm đúng: tổng trọng số các NHÓM = 100 VÀ mỗi nhóm tổng trọng số
/// TIÊU CHÍ = 100 (test <c>EvaluationCriteriaSeedDataTests</c> canh). Admin có thể sửa/thêm/xoá
/// qua API sau khi seed (giữ tổng = 100).
/// </summary>
public static class EvaluationCriteriaSeedData
{
    public static IReadOnlyList<CriteriaGroupSeed> Groups { get; } = new[]
    {
        new CriteriaGroupSeed("Kết quả công việc", 45, new[]
        {
            new MetricSeed("Hoàn thành khối lượng/nhiệm vụ được giao", 60, "Mức độ hoàn thành sản lượng, khối lượng và nhiệm vụ được giao trong kỳ."),
            new MetricSeed("Chất lượng & độ chính xác", 40, "Chất lượng kết quả công việc, tỷ lệ lỗi/tái chế thấp, độ chính xác cao."),
        }),
        new CriteriaGroupSeed("Năng lực & Thái độ", 35, new[]
        {
            new MetricSeed("Chủ động, xử lý vấn đề & cải tiến", 50, "Chủ động trong công việc, xử lý sự cố phát sinh và đề xuất cải tiến."),
            new MetricSeed("Phối hợp & tinh thần đồng đội", 50, "Phối hợp, hỗ trợ đồng nghiệp và các bộ phận liên quan."),
        }),
        new CriteriaGroupSeed("Ý thức kỷ luật", 20, new[]
        {
            new MetricSeed("Chấp hành nội quy, giờ giấc, an toàn", 50, "Đi làm đầy đủ, đúng giờ, chấp hành nội quy và đảm bảo an toàn lao động."),
            new MetricSeed("Tinh thần trách nhiệm", 50, "Trách nhiệm với công việc và kết quả chung của tập thể."),
        }),
    };
}
