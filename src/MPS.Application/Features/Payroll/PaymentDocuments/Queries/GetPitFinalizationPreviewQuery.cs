using MediatR;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.Payroll.PaymentDocuments.Common;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Queries;

/// <summary>
/// Bảng đối chiếu quyết toán thuế TNCN của một năm: mỗi NV một dòng, thuế tính theo biểu NĂM so
/// với tổng thuế đã khấu trừ 12 tháng.
///
/// <para>Cột lệch chính là số NLĐ được hoàn hoặc phải nộp thêm — kế toán cần nhìn con số này trước
/// khi in cả tập tờ khai, vì lệch bất thường thường là dấu hiệu bảng lương một tháng nào đó sai.</para>
/// </summary>
public sealed record GetPitFinalizationPreviewQuery(int Year) : IRequest<PitFinalizationPreviewDto>;

public sealed class GetPitFinalizationPreviewQueryHandler
    : IRequestHandler<GetPitFinalizationPreviewQuery, PitFinalizationPreviewDto>
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly PaymentDocumentContextLoader _loader;

    public GetPitFinalizationPreviewQueryHandler(
        IPayrollPeriodRepository periods, PaymentDocumentContextLoader loader)
    {
        _periods = periods;
        _loader = loader;
    }

    public async Task<PitFinalizationPreviewDto> Handle(
        GetPitFinalizationPreviewQuery request, CancellationToken ct)
    {
        var rows = await _periods.GetRowsByYearAsync(request.Year, null, ct);

        var missing = new List<string>();
        if (await _loader.LoadEmployerAsync(ct) is null)
            missing.Add("Bên ký hợp đồng (tên + mã số thuế + địa chỉ pháp nhân)");
        if (rows.Count == 0)
            missing.Add($"Kỳ lương đã chốt của năm {request.Year}");

        var brackets = PitFinalizationBrackets.Resolve(rows);

        var employees = rows
            .GroupBy(r => r.EmployeeInformationId!.Value)
            .Select(group => MapEmployee(group.Key, group.ToList(), brackets))
            .OrderBy(e => e.EmployeeCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PitFinalizationPreviewDto(request.Year, missing, employees);
    }

    private static PitFinalizationEmployeeDto MapEmployee(
        int employeeInformationId,
        IReadOnlyList<PayrollRow> employeeRows,
        IReadOnlyList<PitBracketSnapshot> brackets)
    {
        var employee = employeeRows[0].EmployeeInformation!;
        var figures = PitFinalizationCalculator.Compute(
            employeeRows, brackets, employee.DependentsNo);

        var annualTax = figures[37] ?? 0m;
        var withheldTax = figures[39] ?? 0m;

        return new PitFinalizationEmployeeDto(
            EmployeeInformationId: employeeInformationId,
            EmployeeCode: employee.User?.EmployeeCode ?? string.Empty,
            FullName: employee.FullName,
            MonthsCount: employeeRows.Select(r => r.PayrollPeriod.PeriodMonth).Distinct().Count(),
            TaxableIncome: figures[19] ?? 0m,
            AnnualTax: annualTax,
            WithheldTax: withheldTax,
            Difference: annualTax - withheldTax);
    }
}

/// <summary>
/// Biểu thuế TNCN dùng để tính lại thuế cả năm.
///
/// <para>Lấy từ SNAPSHOT của kỳ lương mới nhất trong năm, không tra lại Compliance hiện hành:
/// biểu thuế có thể đã đổi sau khi năm kết thúc, mà tờ khai phải khớp đúng số liệu đã tính
/// trong năm đó.</para>
/// </summary>
internal static class PitFinalizationBrackets
{
    public static IReadOnlyList<PitBracketSnapshot> Resolve(IReadOnlyList<PayrollRow> yearRows)
    {
        var latestSnapshot = yearRows
            .OrderByDescending(r => r.PayrollPeriod.PeriodMonth)
            .Select(r => r.PayrollPeriod.PitBracketsSnapshotJson)
            .FirstOrDefault(json => !string.IsNullOrWhiteSpace(json));

        if (latestSnapshot is null) return Array.Empty<PitBracketSnapshot>();

        return PayrollJson.Deserialize<List<PitBracketSnapshot>>(latestSnapshot)
            ?? (IReadOnlyList<PitBracketSnapshot>)Array.Empty<PitBracketSnapshot>();
    }
}
