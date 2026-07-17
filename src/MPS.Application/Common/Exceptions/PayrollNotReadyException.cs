using MPS.Application.Common.Interfaces.Payroll;

namespace MPS.Application.Common.Exceptions;

/// <summary>
/// Ném khi tạo kỳ lương mà còn nhân viên thiếu dữ liệu — chặn cả kỳ. Middleware map 422 kèm danh
/// sách NV thiếu + lý do.
/// </summary>
public sealed class PayrollNotReadyException : Exception
{
    public IReadOnlyList<PayrollEmployeeReadiness> NotReady { get; }

    public PayrollNotReadyException(IReadOnlyList<PayrollEmployeeReadiness> notReady)
        : base("Chưa đủ dữ liệu để tạo bảng lương: có nhân viên thiếu thông tin bắt buộc.")
        => NotReady = notReady;
}
