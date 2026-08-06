namespace MPS.Domain.Enums.Contract;

public enum ContractStatus
{
    Draft,
    Active,
    /// <summary>
    /// Đã có quyết định chấm dứt HĐ hợp lệ nhưng chưa qua ngày làm việc cuối (TerminationDate).
    /// Downstream vận hành (payroll/attendance/BHXH/login) vẫn coi như đang hoạt động;
    /// downstream chỉnh sửa (phụ lục/allowance/tạo termination thứ 2) bị chặn.
    /// Job promote sau ngày TerminationDate (giờ VN) sẽ flip sang <see cref="Terminated"/>.
    /// </summary>
    PendingTermination,
    Expired,
    Terminated,
    Cancelled,
}
