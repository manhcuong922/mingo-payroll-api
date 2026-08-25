using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Entities.Payroll.Banking;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>Ba mảnh dữ liệu mà cả 3 chứng từ đều cần: kỳ lương, cấu hình chi trả, pháp nhân.</summary>
/// <param name="Company">null khi chưa khai bên ký hợp đồng — chứng từ sẽ không có tên/địa chỉ công ty.</param>
public sealed record PaymentDocumentContext(
    PayrollPeriod Period,
    CompanyPaymentSetting? Setting,
    SalaryPaymentCompanyProfile? Company);

/// <summary>
/// Gom việc nạp dữ liệu nền cho chứng từ chi trả.
///
/// <para>Tách ra vì cả 4 handler (2 xem trước + 2 xuất) đều cần đúng bộ ba này; để mỗi handler tự
/// nạp thì rất dễ có chỗ quên <c>GetFullAsync</c> và đọc phải kỳ không có Cells ⇒ mọi số về 0.</para>
/// </summary>
public sealed class PaymentDocumentContextLoader
{
    private readonly IPayrollPeriodRepository _periods;
    private readonly ICompanyPaymentSettingRepository _settings;
    private readonly IContractPartyRepository _parties;

    public PaymentDocumentContextLoader(
        IPayrollPeriodRepository periods,
        ICompanyPaymentSettingRepository settings,
        IContractPartyRepository parties)
    {
        _periods = periods;
        _settings = settings;
        _parties = parties;
    }

    public async Task<PaymentDocumentContext> LoadAsync(int periodId, CancellationToken ct)
    {
        var period = await _periods.GetFullAsync(periodId, asNoTracking: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ lương #{periodId}.");

        return new PaymentDocumentContext(
            period,
            await _settings.GetAsync(ct),
            await LoadCompanyAsync(ct));
    }

    /// <summary>Pháp nhân đang hiệu lực — tên và địa chỉ in ở đầu cả 3 chứng từ.</summary>
    public async Task<SalaryPaymentCompanyProfile?> LoadCompanyAsync(CancellationToken ct)
    {
        var parties = await _parties.GetAllAsync(activeOnly: true, ct);
        var party = parties.FirstOrDefault();

        return party is null ? null : new SalaryPaymentCompanyProfile(party.LegalName, party.Address);
    }

    /// <summary>Pháp nhân đang hiệu lực kèm mã số thuế — tờ khai QTT-TNCN cần cả MST.</summary>
    public async Task<PitFinalizationEmployer?> LoadEmployerAsync(CancellationToken ct)
    {
        var parties = await _parties.GetAllAsync(activeOnly: true, ct);
        var party = parties.FirstOrDefault();

        return party is null
            ? null
            : new PitFinalizationEmployer(party.LegalName, party.TaxCode, party.Address);
    }

    /// <summary>
    /// Tổng phải nộp cơ quan BHXH của kỳ: phần trừ vào lương NLĐ + phần doanh nghiệp đóng.
    ///
    /// <para>KHÔNG cộng KPCĐ: kinh phí công đoàn nộp cho Liên đoàn Lao động, không phải cơ quan
    /// BHXH — gộp vào đây là chuyển thừa tiền sang tài khoản BHXH.</para>
    /// </summary>
    public static (decimal Employee, decimal Employer) SumBhxhContributions(PayrollPeriod period)
    {
        var rows = period.Rows.Where(r => r.RowType == PayrollRowType.Employee).ToList();

        return (
            PayrollCellReader.SumOrZero(rows, PayrollColumnCodes.CongBhNld),
            PayrollCellReader.SumOrZero(rows, PayrollColumnCodes.CongBhDn));
    }
}
