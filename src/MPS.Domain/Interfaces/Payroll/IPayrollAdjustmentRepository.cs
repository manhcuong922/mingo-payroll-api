using MPS.Domain.Entities.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

/// <summary>BS-5 — đọc/ghi các khoản truy lĩnh/truy thu chờ áp vào kỳ lương mới.</summary>
public interface IPayrollAdjustmentRepository
{
    /// <summary>
    /// Các khoản CHƯA áp (<c>AppliedPeriodId = null</c>) của tập NV, sắp theo thời điểm tạo
    /// (khoản cũ được áp trước — quan trọng khi truy thu phải chia nhiều kỳ).
    /// </summary>
    Task<IReadOnlyList<PayrollAdjustment>> GetPendingForEmployeesAsync(
        IReadOnlyCollection<int> employeeInformationIds, CancellationToken ct = default);

    /// <summary>
    /// Y hệt <see cref="GetPendingForEmployeesAsync"/> nhưng trả bản KHÔNG THEO DÕI (AsNoTracking).
    ///
    /// <para>Bắt buộc dùng ở nhánh chỉ-đọc (màn hình tạm tính giữa tháng):
    /// <c>PayrollAdjustmentApplier.Build</c> có SỬA chính các entity này — đặt <c>AppliedAt</c> và
    /// cắt <c>Amount</c> khi trừ một phần. Nếu chúng đang được EF theo dõi thì một lệnh
    /// <c>SaveChanges</c> bất kỳ sau đó sẽ ghi thật những thay đổi đó xuống DB, tức là bấm "xem thử"
    /// lại đánh dấu khoản truy thu là ĐÃ TRỪ trong khi chưa có kỳ lương nào trả tiền.</para>
    /// </summary>
    Task<IReadOnlyList<PayrollAdjustment>> GetPendingForEmployeesReadOnlyAsync(
        IReadOnlyCollection<int> employeeInformationIds, CancellationToken ct = default);

    /// <summary>Các khoản đã áp vào 1 kỳ — dùng khi xoá kỳ để trả chúng về trạng thái chưa áp.</summary>
    Task<IReadOnlyList<PayrollAdjustment>> GetAppliedToPeriodAsync(int periodId, CancellationToken ct = default);

    /// <summary>
    /// Các khoản CHƯA áp phát sinh TỪ chính kỳ này cho 1 NV — dùng cho sàn 0 (#44): mỗi lượt tính
    /// lại phải THAY THẾ khoản thiếu cũ, không được cộng dồn qua từng lần bấm "tính lại".
    /// </summary>
    Task<IReadOnlyList<PayrollAdjustment>> GetPendingFromSourcePeriodAsync(
        int sourcePeriodId, int employeeInformationId, CancellationToken ct = default);

    /// <summary>Mọi khoản CHƯA áp phát sinh từ 1 kỳ — dùng khi xoá kỳ đó (#44).</summary>
    Task<IReadOnlyList<PayrollAdjustment>> GetPendingSourcedByPeriodAsync(
        int sourcePeriodId, CancellationToken ct = default);

    void Add(PayrollAdjustment adjustment);

    void Remove(PayrollAdjustment adjustment);

    Task SaveChangesAsync(CancellationToken ct = default);
}
