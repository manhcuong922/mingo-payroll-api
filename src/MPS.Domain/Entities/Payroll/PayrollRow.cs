using MPS.Domain.Enums.Payroll;

namespace MPS.Domain.Entities.Payroll;

/// <summary>1 dòng bảng lương: mỗi nhân viên 1 dòng (Employee) + 1 dòng tổng cộng (Total).</summary>
public class PayrollRow
{
    public int Id { get; set; }

    public int PayrollPeriodId { get; set; }
    public PayrollPeriod PayrollPeriod { get; set; } = null!;

    public PayrollRowType RowType { get; set; } = PayrollRowType.Employee;

    /// <summary>Null cho dòng Total.</summary>
    public int? EmployeeInformationId { get; set; }
    public EmployeeInformation? EmployeeInformation { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>
    /// Snapshot field gốc của NV (Numbers + Texts) tại lúc tạo kỳ — để recompute sau khi override
    /// không cần đọc lại module (bất biến). Null cho dòng Total.
    /// </summary>
    public string? InputSnapshotJson { get; set; }

    public ICollection<PayrollCellValue> Cells { get; set; } = new List<PayrollCellValue>();
}
