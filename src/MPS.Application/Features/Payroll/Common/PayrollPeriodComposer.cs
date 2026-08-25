using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// Dựng TOÀN BỘ các dòng của một kỳ lương (mỗi NV một dòng + dòng tổng) từ liệu đã gom.
///
/// <para><b>Vì sao tách khỏi handler.</b> Có hai chỗ cần đúng y hệt nhau: lệnh TẠO KỲ (ghi DB) và
/// màn hình TẠM TÍNH giữa tháng (chỉ đọc). Cả hai phải cho ra con số giống nhau tuyệt đối — tạm
/// tính mà lệch với kỳ thật thì tệ hơn là không có tạm tính. Chép hai bản là bảo đảm sẽ lệch ở lần
/// sửa công thức tiếp theo.</para>
///
/// <para><b>Không đụng DB.</b> Lớp này chỉ nhận dữ liệu và trả về kết quả, kể cả các khoản truy
/// lĩnh/truy thu phát sinh — bên gọi tự quyết định LƯU hay VỨT. Nhờ vậy nhánh tạm tính không thể
/// vô tình ghi gì vào <c>PayrollAdjustments</c>.</para>
///
/// <para>⚠️ <see cref="PayrollAdjustmentApplier.Build"/> có SỬA chính các entity khoản điều chỉnh
/// truyền vào (đặt <c>AppliedAt</c>, cắt <c>Amount</c> khi trừ một phần). Bên gọi ở nhánh chỉ-đọc
/// bắt buộc phải nạp chúng bằng <c>AsNoTracking</c>, nếu không EF sẽ lưu những thay đổi đó ở lần
/// <c>SaveChanges</c> bất kỳ sau đó.</para>
/// </summary>
public static class PayrollPeriodComposer
{
    /// <param name="CarriedAdjustments">
    /// Bản ghi phần dư của khoản truy thu bị trừ một phần — CẦN thêm mới vào repository.
    /// </param>
    /// <param name="ShortfallAdjustments">
    /// Khoản thiếu do sàn 0 (#44) của từng dòng — CẦN thêm mới vào repository.
    /// </param>
    public sealed record Result(
        IReadOnlyList<PayrollAdjustment> CarriedAdjustments,
        IReadOnlyList<PayrollAdjustment> ShortfallAdjustments);

    /// <summary>
    /// Tính mọi dòng và ĐỔ THẲNG vào <paramref name="period"/>.<c>Rows</c> (dòng NV theo mã NV, rồi
    /// dòng tổng ở cuối).
    /// </summary>
    /// <param name="pendingByEmp">Khoản truy lĩnh/truy thu chưa áp, gom theo NV.</param>
    public static Result Compose(
        PayrollPeriod period,
        PayrollCollectionResult collection,
        PayrollColumnPlan plan,
        IPayrollCalculator calculator,
        IPayrollFieldCatalog catalog,
        IReadOnlyDictionary<int, IReadOnlyList<PayrollAdjustment>> pendingByEmp,
        int userId,
        DateTime nowUtc)
    {
        var carried = new List<PayrollAdjustment>();
        var shortfalls = new List<PayrollAdjustment>();

        var ordered = collection.Employees
            .OrderBy(e => e.Texts.GetValueOrDefault(PayrollFieldKeys.EmployeeCode, string.Empty))
            .ToList();

        var employeeRows = new List<PayrollRow>();
        var order = 0;
        foreach (var inputs in ordered)
        {
            var row = new PayrollRow
            {
                RowType = PayrollRowType.Employee,
                EmployeeInformationId = inputs.EmployeeInformationId,
                DisplayOrder = order++,
            };

            var pending = pendingByEmp.GetValueOrDefault(inputs.EmployeeInformationId);

            // Truy lĩnh (+) áp trọn ngay ở lượt tính đầu; truy thu (−) cần biết THỰC NHẬN trước khi
            // trừ (không để âm) nên chỉ chốt ở lượt hai.
            inputs.Numbers[PayrollFieldKeys.TruyLinh] =
                pending is null ? 0m : PayrollAdjustmentApplier.SumPositives(pending);
            inputs.Numbers[PayrollFieldKeys.TruyThu] = 0m;

            var ctx = new PayrollCalcContext
            {
                Fields = inputs.ToFields(catalog),
                FunctionContext = collection.FunctionContext,
            };
            calculator.ComputeAll(plan, ctx, row);

            if (pending is not null)
            {
                var netBeforeClawback = EffectiveNumber(row, PayrollColumnCodes.ThucNhan);
                var applyPlan = PayrollAdjustmentApplier.Build(pending, netBeforeClawback, nowUtc);

                foreach (var a in applyPlan.Applied) a.AppliedPeriod = period;
                foreach (var c in applyPlan.Carried)
                {
                    c.AppliedPeriod = period;
                    carried.Add(c);
                }

                if (applyPlan.TruyThu != 0m)
                {
                    inputs.Numbers[PayrollFieldKeys.TruyThu] = applyPlan.TruyThu;
                    ctx = new PayrollCalcContext
                    {
                        Fields = inputs.ToFields(catalog),
                        FunctionContext = collection.FunctionContext,
                    };
                    calculator.ComputeAll(plan, ctx, row);
                }
            }

            // #44 — sàn 0: sau khi đã chốt truy lĩnh/truy thu, thực nhận vẫn có thể âm (BH tính
            // trên MUC_LUONG nguyên trong khi thu nhập chia theo công chuẩn). Kẹp về 0 và đẩy
            // phần thiếu sang kỳ sau, đúng nguyên tắc đã áp cho truy thu.
            var shortfall = PayrollNetFloor.Apply(row, period, userId, nowUtc);
            if (shortfall is not null) shortfalls.Add(shortfall);

            // Snapshot SAU khi đã chốt số truy lĩnh/truy thu — recompute sau này tái lập đúng số.
            row.InputSnapshotJson = inputs.ToSnapshotJson();

            employeeRows.Add(row);
            period.Rows.Add(row);
        }

        var totalRow = new PayrollRow { RowType = PayrollRowType.Total, DisplayOrder = order };
        calculator.ComputeTotals(plan, employeeRows, totalRow);
        period.Rows.Add(totalRow);

        return new Result(carried, shortfalls);
    }

    private static decimal EffectiveNumber(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c => c.ColumnCode == columnCode);
        if (cell is null) return 0m;
        return (cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue) ?? 0m;
    }
}
