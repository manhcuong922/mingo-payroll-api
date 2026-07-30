using System.Globalization;
using System.Text;
using MediatR;
using MPS.Application.Features.Payroll.Queries;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.BhxhTaxPayment.Queries;

/// <summary>
/// Xuất toàn bộ line của 1 obligation ra file để đối chiếu với biểu mẫu BHXH.
/// v1 dùng CSV UTF-8 với BOM (Excel VN mở trực tiếp) — nhẹ hơn OpenXml cho use case đối chiếu.
/// Columns: Mã NV | Họ tên | Đóng NLD | Đóng DN | Tổng | Trạng thái | Ngày đóng | Số thực đóng
/// | Hình thức | Mã GD | Lý do bỏ qua. Row Total ở cuối.
/// </summary>
public sealed record ExportPaymentObligationLinesQuery(int ObligationId) : IRequest<PayrollExportFileDto>;

public sealed class ExportPaymentObligationLinesQueryHandler
    : IRequestHandler<ExportPaymentObligationLinesQuery, PayrollExportFileDto>
{
    private const string CsvContentType = "text/csv; charset=utf-8";
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private readonly IPaymentObligationRepository _repo;

    public ExportPaymentObligationLinesQueryHandler(IPaymentObligationRepository repo) => _repo = repo;

    public async Task<PayrollExportFileDto> Handle(ExportPaymentObligationLinesQuery request, CancellationToken ct)
    {
        var obligation = await _repo.GetByIdAsync(request.ObligationId, asNoTracking: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy nghĩa vụ nộp #{request.ObligationId}.");
        var lines = await _repo.GetAllLinesByObligationAsync(request.ObligationId, ct);

        var sb = new StringBuilder();
        sb.Append('﻿'); // UTF-8 BOM để Excel VN detect encoding.
        sb.AppendLine("Mã NV,Họ tên,Đóng NLD,Đóng DN,Tổng,Trạng thái,Ngày đóng,Số thực đóng,Hình thức,Mã GD,Lý do bỏ qua");

        decimal sumEmployee = 0m, sumEmployer = 0m, sumTotal = 0m, sumPaid = 0m;
        foreach (var line in lines)
        {
            sumEmployee += line.EmployeeContribution;
            sumEmployer += line.EmployerContribution;
            sumTotal += line.TotalAmount;
            sumPaid += line.PaidAmount ?? 0m;

            sb.Append(Csv(line.EmployeeCodeSnapshot)).Append(',')
              .Append(Csv(line.EmployeeFullNameSnapshot)).Append(',')
              .Append(line.EmployeeContribution.ToString(Culture)).Append(',')
              .Append(line.EmployerContribution.ToString(Culture)).Append(',')
              .Append(line.TotalAmount.ToString(Culture)).Append(',')
              .Append(StatusLabel(line.LineStatus)).Append(',')
              .Append(line.PaidDate?.ToString("yyyy-MM-dd", Culture) ?? string.Empty).Append(',')
              .Append(line.PaidAmount?.ToString(Culture) ?? string.Empty).Append(',')
              .Append(MethodLabel(line.PaymentMethod)).Append(',')
              .Append(Csv(line.TransactionRef ?? string.Empty)).Append(',')
              .Append(Csv(line.ExcludedReason ?? string.Empty))
              .AppendLine();
        }

        sb.Append("TỔNG,,")
          .Append(sumEmployee.ToString(Culture)).Append(',')
          .Append(sumEmployer.ToString(Culture)).Append(',')
          .Append(sumTotal.ToString(Culture)).Append(",,,")
          .Append(sumPaid.ToString(Culture))
          .AppendLine(",,,");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"nghia-vu-nop-{obligation.ObligationType}-{obligation.Id}.csv";
        return new PayrollExportFileDto(bytes, fileName, CsvContentType);
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        var escaped = value.Replace("\"", "\"\"");
        return needQuote ? $"\"{escaped}\"" : escaped;
    }

    private static string StatusLabel(PaymentLineStatus s) => s switch
    {
        PaymentLineStatus.Pending => "Chưa xử lý",
        PaymentLineStatus.Paid => "Đã nộp",
        PaymentLineStatus.Excluded => "Bỏ qua",
        _ => s.ToString(),
    };

    private static string MethodLabel(PaymentMethod? m) => m switch
    {
        PaymentMethod.BankTransfer => "Chuyển khoản",
        PaymentMethod.DirectPayment => "Nộp trực tiếp",
        PaymentMethod.BhxhOnline => "BHXH Online",
        PaymentMethod.ETax => "eTax",
        _ => string.Empty,
    };
}
