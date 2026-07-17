using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Common.Interfaces.Payroll;

public sealed record PayrollCollectionRequest(
    int Year,
    int Month,
    DateOnly PayDate,
    decimal StandardWorkingDays,
    IReadOnlyList<int> EmployeeInformationIds);

/// <summary>
/// Kết quả gom liệu: input mỗi NV (đã nhúng field compliance/attendance/OT/eval/param) + refs compliance
/// (để snapshot + diff luật) + ngữ cảnh hàm (biểu thuế) + tham số + JSON snapshot cho kỳ.
/// </summary>
public sealed class PayrollCollectionResult
{
    public required IReadOnlyList<PayrollEmployeeInputs> Employees { get; init; }
    public required IReadOnlyList<PayrollComplianceRef> ComplianceRefs { get; init; }
    public required PayrollFunctionContext FunctionContext { get; init; }
    public required IReadOnlyDictionary<string, ResolvedValue> ParamValues { get; init; }
    public required string ParamsSnapshotJson { get; init; }
    public required string ProductivitySnapshotJson { get; init; }
}

/// <summary>
/// Gom toàn bộ dữ liệu 1 kỳ lương từ các module: Hồ sơ/HĐ (lương gốc, cờ, phụ cấp), Chấm công (4 loại
/// công suy theo giai đoạn HĐ/ngày), Tăng ca, Đánh giá (điểm→hệ số), Compliance (rate/trần/giảm trừ theo payDate).
/// </summary>
public interface IPayrollDataCollector
{
    Task<PayrollCollectionResult> CollectAsync(PayrollCollectionRequest request, CancellationToken cancellationToken);
}
