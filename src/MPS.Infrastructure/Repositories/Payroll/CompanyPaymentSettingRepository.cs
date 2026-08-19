using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Payroll.Banking;
using MPS.Domain.Interfaces.Payroll;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Payroll;

public sealed class CompanyPaymentSettingRepository : ICompanyPaymentSettingRepository
{
    private readonly AppDbContext _context;

    public CompanyPaymentSettingRepository(AppDbContext context) => _context = context;

    public async Task<CompanyPaymentSetting?> GetAsync(CancellationToken cancellationToken = default)
        => await _context.CompanyPaymentSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CompanyPaymentSetting> UpsertAsync(
        CompanyPaymentSetting setting, CancellationToken cancellationToken = default)
    {
        // Singleton: nếu đã có dòng thì luôn cập nhật dòng đó, không bao giờ chèn thêm.
        // DB còn một unique index chặn dòng thứ hai để phòng hai request đồng thời.
        var existing = await _context.CompanyPaymentSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            _context.CompanyPaymentSettings.Add(setting);
            await _context.SaveChangesAsync(cancellationToken);
            return setting;
        }

        existing.SalaryBankCode = setting.SalaryBankCode;
        existing.SalaryBankBranch = setting.SalaryBankBranch;
        existing.SalaryAccountNo = setting.SalaryAccountNo;
        existing.SalaryAccountName = setting.SalaryAccountName;
        existing.SalaryCustomerCif = setting.SalaryCustomerCif;
        existing.SalaryContactPhone = setting.SalaryContactPhone;

        existing.BhxhUnitCode = setting.BhxhUnitCode;
        existing.BhxhAgencyCode = setting.BhxhAgencyCode;
        existing.BhxhAgencyName = setting.BhxhAgencyName;
        existing.BhxhPayerBankCode = setting.BhxhPayerBankCode;
        existing.BhxhPayerBankBranch = setting.BhxhPayerBankBranch;
        existing.BhxhPayerAccountNo = setting.BhxhPayerAccountNo;
        existing.BhxhBeneficiaryAccountNo = setting.BhxhBeneficiaryAccountNo;
        existing.BhxhBeneficiaryBankName = setting.BhxhBeneficiaryBankName;

        existing.UpdatedAt = setting.UpdatedAt;
        existing.UpdatedByUserId = setting.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<IReadOnlyList<BankCatalog>> GetBanksAsync(CancellationToken cancellationToken = default)
        => await _context.BankCatalogs
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.IsForeign).ThenBy(b => b.Code)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProvinceCatalog>> GetProvincesAsync(CancellationToken cancellationToken = default)
        => await _context.ProvinceCatalogs
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);

    public async Task<BankCatalog?> FindBankAsync(string? code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        return await _context.BankCatalogs
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Code == code, cancellationToken);
    }
}
