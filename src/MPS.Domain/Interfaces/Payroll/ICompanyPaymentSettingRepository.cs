using MPS.Domain.Entities.Payroll.Banking;

namespace MPS.Domain.Interfaces.Payroll;

/// <summary>Truy cập cấu hình chi trả cấp công ty (singleton) và 2 danh mục ngân hàng/tỉnh.</summary>
public interface ICompanyPaymentSettingRepository
{
    /// <summary>Bản ghi cấu hình duy nhất, hoặc null khi công ty chưa khai.</summary>
    Task<CompanyPaymentSetting?> GetAsync(CancellationToken cancellationToken = default);

    Task<CompanyPaymentSetting> UpsertAsync(
        CompanyPaymentSetting setting, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankCatalog>> GetBanksAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProvinceCatalog>> GetProvincesAsync(CancellationToken cancellationToken = default);

    /// <summary>Tra tên hiển thị của ngân hàng theo mã; null khi mã không có trong danh mục.</summary>
    Task<BankCatalog?> FindBankAsync(string? code, CancellationToken cancellationToken = default);
}
