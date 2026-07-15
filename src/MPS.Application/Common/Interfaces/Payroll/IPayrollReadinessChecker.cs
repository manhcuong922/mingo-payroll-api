namespace MPS.Application.Common.Interfaces.Payroll;

/// <summary>Yêu cầu kiểm tra đủ liệu cho 1 kỳ lương (công ty + tháng).</summary>
public sealed record PayrollReadinessRequest(int EmployerPartyId, int Year, int Month);

/// <summary>Kết quả đủ liệu 1 nhân viên. <c>Missing</c> rỗng ⇒ đủ.</summary>
public sealed record PayrollEmployeeReadiness(
    int EmployeeInformationId,
    string EmployeeCode,
    string FullName,
    IReadOnlyList<string> Missing)
{
    public bool IsReady => Missing.Count == 0;
}

/// <summary>
/// Xác định danh sách NV trong phạm vi kỳ (có HĐ hiệu lực ở công ty đó) và kiểm 4 điều kiện đủ liệu:
/// HĐ hiệu lực, chấm công + OT đã chốt, đánh giá tháng Approved, hồ sơ chi lương đủ. Thiếu 1 → chặn cả kỳ.
/// </summary>
public interface IPayrollReadinessChecker
{
    Task<IReadOnlyList<PayrollEmployeeReadiness>> CheckAsync(PayrollReadinessRequest request, CancellationToken cancellationToken);
}
