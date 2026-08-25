using MediatR;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.CompanyPaymentSettings.Queries;

/// <summary>Danh mục ngân hàng + tỉnh/TP để chọn khi khai tài khoản (hồ sơ NV, cấu hình chi trả).</summary>
public sealed record GetBankingCatalogsQuery : IRequest<BankingCatalogsDto>;

public sealed class GetBankingCatalogsQueryHandler
    : IRequestHandler<GetBankingCatalogsQuery, BankingCatalogsDto>
{
    private readonly ICompanyPaymentSettingRepository _repository;

    public GetBankingCatalogsQueryHandler(ICompanyPaymentSettingRepository repository)
        => _repository = repository;

    public async Task<BankingCatalogsDto> Handle(GetBankingCatalogsQuery request, CancellationToken ct)
    {
        var banks = await _repository.GetBanksAsync(ct);
        var provinces = await _repository.GetProvincesAsync(ct);

        return new BankingCatalogsDto(
            banks.Select(b => new BankCatalogDto(b.Code, b.FullName, b.IsForeign)).ToList(),
            provinces.Select(p => new ProvinceCatalogDto(p.Code, p.Name, p.FormName)).ToList());
    }
}
