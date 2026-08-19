using MediatR;
using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.API.Requests.Payroll;
using MPS.Application.Features.Payroll.CompanyPaymentSettings.Commands;
using MPS.Application.Features.Payroll.CompanyPaymentSettings.Queries;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

/// <summary>
/// Cấu hình chi trả cấp công ty (bản ghi duy nhất) + 2 danh mục ngân hàng/tỉnh dùng để chọn.
///
/// <para>Sửa cấu hình là đổi được tài khoản nhận tiền trên mọi chứng từ về sau, nên quyền sửa
/// hẹp hơn quyền xem.</para>
/// </summary>
[ApiController]
[Route("api/company-payment-setting")]
public class CompanyPaymentSettingController : ControllerBase
{
    private readonly IMediator _mediator;
    public CompanyPaymentSettingController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(Permissions.CompanyPaymentSettingReadAll)]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCompanyPaymentSettingQuery(), ct));

    [HttpPut]
    [HasPermission(Permissions.CompanyPaymentSettingUpdateAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateCompanyPaymentSettingRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateCompanyPaymentSettingCommand(
            SalaryBankCode: request.SalaryBankCode,
            SalaryBankBranch: request.SalaryBankBranch,
            SalaryAccountNo: request.SalaryAccountNo,
            SalaryAccountName: request.SalaryAccountName,
            SalaryCustomerCif: request.SalaryCustomerCif,
            SalaryContactPhone: request.SalaryContactPhone,
            BhxhUnitCode: request.BhxhUnitCode,
            BhxhAgencyCode: request.BhxhAgencyCode,
            BhxhAgencyName: request.BhxhAgencyName,
            BhxhPayerBankCode: request.BhxhPayerBankCode,
            BhxhPayerBankBranch: request.BhxhPayerBankBranch,
            BhxhPayerAccountNo: request.BhxhPayerAccountNo,
            BhxhBeneficiaryAccountNo: request.BhxhBeneficiaryAccountNo,
            BhxhBeneficiaryBankName: request.BhxhBeneficiaryBankName), ct);

        return NoContent();
    }

    /// <summary>
    /// Danh mục ngân hàng + tỉnh/TP. Mở cho cả người sửa hồ sơ nhân sự: form khai tài khoản của NV
    /// phải chọn từ đúng danh mục này, gõ tay tên viết tắt sai là ngân hàng trả lại cả lệnh chi.
    /// </summary>
    [HttpGet("catalogs")]
    [HasAnyPermission(
        Permissions.CompanyPaymentSettingReadAll,
        Permissions.PayrollDocumentExportAll,
        Permissions.EmployeeInformationUpdateAll,
        Permissions.EmployeeInformationCreateAll)]
    public async Task<IActionResult> GetCatalogs(CancellationToken ct)
        => Ok(await _mediator.Send(new GetBankingCatalogsQuery(), ct));
}
