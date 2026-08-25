using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Constants;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ContractRepository : IContractRepository
{
    private readonly AppDbContext _context;

    public ContractRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Domain.Entities.Contract?> GetByIdAsync(
        int id,
        bool includeAllowances = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Contracts
            .AsNoTracking()
            .Include(c => c.EmployeeInformation)
            .Include(c => c.EmployerParty)
            .Include(c => c.EmployerSignerUser)
            .Include(c => c.SalaryGrade)
            .AsQueryable();

        if (includeAllowances)
        {
            query = query
                .Include(c => c.ContractAllowances)
                    .ThenInclude(a => a.AllowanceType);
        }

        return query.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Domain.Entities.Contract> Items, int Total)> GetPagedAsync(
        string? keyword,
        int? employeeInformationId,
        ContractStatus? status,
        ContractCategory? category,
        ContractKind? kind,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Contracts
            .AsNoTracking()
            .Include(c => c.EmployeeInformation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            var pattern = $"%{kw}%";
            query = query.Where(c =>
                EF.Functions.Like(c.ContractNo, pattern) ||
                EF.Functions.Like(c.EmployeeInformation.FullName, pattern));
        }

        if (employeeInformationId.HasValue)
            query = query.Where(c => c.EmployeeInformationId == employeeInformationId.Value);
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);
        if (category.HasValue)
            query = query.Where(c => c.ContractCategory == category.Value);
        if (kind.HasValue)
            query = query.Where(c => c.Kind == kind.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.EffectiveFrom)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<bool> ContractNoExistsAsync(string contractNo, int? excludeId, CancellationToken cancellationToken = default)
    {
        var normalized = contractNo.Trim();
        var query = _context.Contracts.AsNoTracking().Where(c => c.ContractNo == normalized);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public Task<int> CountDefiniteOfficialContractsAsync(
        int employeeInformationId,
        int employerPartyId,
        int? excludeContractId,
        CancellationToken cancellationToken = default)
    {
        // Điều 20 k.2c chỉ áp dụng cho HĐLĐ toàn thời gian — HĐ dịch vụ/khoán/CTV không tính vào giới hạn này.
        // PendingTermination cũng tính vì HĐ đã ký hợp lệ, chỉ chờ hiệu lực chấm dứt.
        var query = _context.Contracts.AsNoTracking()
            .Where(c => c.EmployeeInformationId == employeeInformationId
                && c.EmployerPartyId == employerPartyId
                && c.Kind == ContractKind.LaborFullTime
                && c.ContractCategory == ContractCategory.Definite
                && c.ContractSubtype == ContractSubtype.Official
                && (c.Status == ContractStatus.Active
                    || c.Status == ContractStatus.PendingTermination
                    || c.Status == ContractStatus.Expired
                    || c.Status == ContractStatus.Terminated));

        if (excludeContractId.HasValue)
            query = query.Where(c => c.Id != excludeContractId.Value);

        return query.CountAsync(cancellationToken);
    }

    public async Task<int> AddAsync(Domain.Entities.Contract contract, IEnumerable<ContractAllowance> allowances, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        await _context.Contracts.AddAsync(contract, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var allowance in allowances)
        {
            allowance.ContractId = contract.Id;
            await _context.ContractAllowances.AddAsync(allowance, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return contract.Id;
    }

    public async Task<bool> UpdateDraftAsync(Domain.Entities.Contract contract, IEnumerable<ContractAllowance> allowances, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var tracked = await _context.Contracts
            .Include(c => c.ContractAllowances)
            .FirstOrDefaultAsync(c => c.Id == contract.Id, cancellationToken);

        if (tracked is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        if (tracked.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot update contract {contract.Id}: status is {tracked.Status}, only Draft can be modified.");

        // Copy fields onto the tracked entity (avoid attaching a fresh detached graph).
        ApplyScalarFields(tracked, contract);

        // Replace allowances: delete-all + insert-all (simplest, correct for draft state).
        _context.ContractAllowances.RemoveRange(tracked.ContractAllowances);
        foreach (var allowance in allowances)
        {
            allowance.ContractId = tracked.Id;
            await _context.ContractAllowances.AddAsync(allowance, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteDraftAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Contracts
            .Include(c => c.ContractAllowances)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot delete contract {id}: status is {existing.Status}, only Draft can be hard-deleted.");

        _context.ContractAllowances.RemoveRange(existing.ContractAllowances);
        _context.Contracts.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelDraftAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot cancel contract {id}: status is {existing.Status}, only Draft can be cancelled.");

        existing.Status = ContractStatus.Cancelled;
        existing.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<ContractAllowance?> GetAllowanceAsync(int contractId, int allowanceTypeId, CancellationToken cancellationToken = default) =>
        _context.ContractAllowances
            .AsNoTracking()
            .Include(a => a.AllowanceType)
            .FirstOrDefaultAsync(a => a.ContractId == contractId && a.AllowanceTypeId == allowanceTypeId, cancellationToken);

    public async Task<bool> AddAllowanceAsync(int contractId, ContractAllowance allowance, CancellationToken cancellationToken = default)
    {
        var exists = await _context.ContractAllowances
            .AnyAsync(a => a.ContractId == contractId && a.AllowanceTypeId == allowance.AllowanceTypeId, cancellationToken);
        if (exists)
            throw new DomainValidationException(
                $"AllowanceTypeId {allowance.AllowanceTypeId} already exists for contract {contractId}.");

        allowance.ContractId = contractId;
        await _context.ContractAllowances.AddAsync(allowance, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateAllowanceAmountAsync(int contractId, int allowanceTypeId, decimal amount, string currency, string? note, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ContractAllowances
            .FirstOrDefaultAsync(a => a.ContractId == contractId && a.AllowanceTypeId == allowanceTypeId, cancellationToken);
        if (existing is null) return false;

        existing.Amount = amount;
        existing.Currency = currency;
        existing.Note = note;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAllowanceAsync(int contractId, int allowanceTypeId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ContractAllowances
            .FirstOrDefaultAsync(a => a.ContractId == contractId && a.AllowanceTypeId == allowanceTypeId, cancellationToken);
        if (existing is null) return false;

        _context.ContractAllowances.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> ActivateDraftAsync(
        int contractId,
        decimal snapshotAmount,
        string snapshotCurrency,
        DateTime signedAtUtc,
        string signedPlace,
        ContractFile file,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var tracked = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken)
            ?? throw new DomainValidationException($"Contract {contractId} not found.");

        if (tracked.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {contractId} is archived and read-only.");

        if (tracked.Status != ContractStatus.Draft)
            throw new DomainValidationException(
                $"Cannot activate contract {contractId}: status is {tracked.Status}, only Draft can be activated.");

        await EnsureNoOverlappingActiveContractAsync(tracked, cancellationToken);

        // Freeze snapshot + activate
        tracked.SalaryAmountSnapshot = snapshotAmount;
        tracked.SalaryCurrencySnapshot = snapshotCurrency;
        tracked.SignedAt = signedAtUtc;
        tracked.SignedPlace = signedPlace;
        tracked.Status = ContractStatus.Active;
        tracked.UpdatedAt = DateTime.UtcNow;

        // Insert ContractFile (V1, IsCurrent)
        file.TargetId = contractId;
        file.TargetType = FileTargetType.Contract;
        file.Version = 1;
        file.IsCurrent = true;
        await _context.ContractFiles.AddAsync(file, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return file.Id;
    }

    /// <summary>
    /// Một người lao động chỉ được có MỘT hợp đồng hiệu lực tại một thời điểm (Đ.20 BLLĐ 2019 —
    /// hợp đồng sau chỉ bắt đầu khi hợp đồng trước đã kết thúc).
    ///
    /// ⚠️ Thiếu guard này thì hai HĐ Active chồng khoảng ngày cùng tồn tại (đo 2026-07-22:
    /// 2027-01-01→2027-12-31 và 2027-06-01→2028-05-31 của cùng một người đều lên Active) và
    /// bảng lương lấy mức lương/phụ cấp theo hợp đồng nào là KHÔNG XÁC ĐỊNH — sai tiền lương
    /// mà không có dấu hiệu nào để phát hiện.
    ///
    /// HĐ chưa có ngày kết thúc (không xác định thời hạn) coi như kéo dài vô hạn.
    /// </summary>
    private async Task EnsureNoOverlappingActiveContractAsync(
        Contract candidate,
        CancellationToken cancellationToken)
    {
        // HĐ đang PendingTermination vẫn chiếm khoảng hiệu lực (NLĐ đi làm) — HĐ mới không được
        // overlap khoảng đó.
        var others = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.EmployeeInformationId == candidate.EmployeeInformationId
                && c.Id != candidate.Id
                && (c.Status == ContractStatus.Active
                    || c.Status == ContractStatus.PendingTermination)
                && c.ArchivedAt == null)
            .Select(c => new { c.Id, c.ContractNo, c.EffectiveFrom, c.EffectiveTo })
            .ToListAsync(cancellationToken);

        var overlapped = others.FirstOrDefault(o =>
            candidate.EffectiveFrom <= (o.EffectiveTo ?? DateTime.MaxValue)
            && o.EffectiveFrom <= (candidate.EffectiveTo ?? DateTime.MaxValue));

        if (overlapped is null) return;

        throw new DomainValidationException(
            $"CONTRACT_OVERLAP: Nhân viên đã có hợp đồng đang hiệu lực '{overlapped.ContractNo}' "
            + $"({overlapped.EffectiveFrom:dd/MM/yyyy}–{(overlapped.EffectiveTo.HasValue ? overlapped.EffectiveTo.Value.ToString("dd/MM/yyyy") : "không thời hạn")}) "
            + "chồng khoảng thời gian với hợp đồng này. Mỗi thời điểm chỉ được một hợp đồng hiệu lực — "
            + "hãy chấm dứt hợp đồng cũ trước, hoặc chỉnh lại ngày hiệu lực.");
    }

    public Task<ContractFile?> GetCurrentFileAsync(
        int contractId,
        FileTargetType targetType,
        CancellationToken cancellationToken = default) =>
        _context.ContractFiles
            .AsNoTracking()
            .Where(f => f.TargetType == targetType && f.TargetId == contractId && f.IsCurrent)
            .OrderByDescending(f => f.Version)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> ReplaceCurrentFileAsync(
        int contractId,
        FileTargetType targetType,
        ContractFile newFile,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken)
            ?? throw new DomainValidationException($"Contract {contractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {contractId} is archived and read-only.");

        var currents = await _context.ContractFiles
            .Where(f => f.TargetType == targetType && f.TargetId == contractId && f.IsCurrent)
            .ToListAsync(cancellationToken);

        var nextVersion = 1;
        foreach (var existing in currents)
        {
            existing.IsCurrent = false;
            if (existing.Version >= nextVersion) nextVersion = existing.Version + 1;
        }

        newFile.TargetId = contractId;
        newFile.TargetType = targetType;
        newFile.Version = nextVersion;
        newFile.IsCurrent = true;
        await _context.ContractFiles.AddAsync(newFile, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return newFile.Id;
    }

    public async Task<IReadOnlyList<ContractFile>> GetFilesByTargetAsync(
        FileTargetType targetType,
        int targetId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ContractFiles
            .AsNoTracking()
            .Where(f => f.TargetType == targetType && f.TargetId == targetId)
            .OrderByDescending(f => f.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> AddContractFileForActiveContractAsync(
        int contractId,
        ContractFile file,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken)
            ?? throw new DomainValidationException($"Contract {contractId} not found.");

        if (contract.ArchivedAt is not null)
            throw new DomainValidationException($"CONTRACT_ARCHIVED: Contract {contractId} is archived and read-only.");

        if (contract.Status != ContractStatus.Active)
            throw new DomainValidationException(
                $"Cannot upload notice for contract {contractId}: status is {contract.Status}, expected Active.");

        var currents = await _context.ContractFiles
            .Where(f => f.TargetType == file.TargetType && f.TargetId == file.TargetId && f.IsCurrent)
            .ToListAsync(cancellationToken);

        var nextVersion = 1;
        foreach (var existing in currents)
        {
            existing.IsCurrent = false;
            if (existing.Version >= nextVersion) nextVersion = existing.Version + 1;
        }

        file.Version = nextVersion;
        file.IsCurrent = true;
        await _context.ContractFiles.AddAsync(file, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return file.Id;
    }

    public Task<DateTime?> GetArchivedAtAsync(int contractId, CancellationToken cancellationToken = default) =>
        _context.Contracts
            .AsNoTracking()
            .Where(c => c.Id == contractId)
            .Select(c => c.ArchivedAt)
            .FirstOrDefaultAsync(cancellationToken);

    // "Active contract" ở góc nhìn vận hành = Active HOẶC PendingTermination — NLĐ trong khoảng
    // đợi chấm dứt vẫn đi làm, chấm công, hưởng lương, nhận task, được provisioned account.
    public Task<bool> HasActiveContractAsync(int employeeInformationId, CancellationToken cancellationToken = default) =>
        _context.Contracts
            .AsNoTracking()
            .AnyAsync(c => c.EmployeeInformationId == employeeInformationId
                        && (c.Status == ContractStatus.Active
                            || c.Status == ContractStatus.PendingTermination), cancellationToken);

    public async Task<IReadOnlyList<ContractKind>> GetActiveContractKindsAsync(
        int employeeInformationId, CancellationToken cancellationToken = default) =>
        await _context.Contracts
            .AsNoTracking()
            .Where(c => c.EmployeeInformationId == employeeInformationId
                        && (c.Status == ContractStatus.Active
                            || c.Status == ContractStatus.PendingTermination))
            .Select(c => c.Kind)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetEmployeeInformationIdsWithActiveContractsAsync(
        IReadOnlyCollection<int> employeeInformationIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeInformationIds.Count == 0)
            return Array.Empty<int>();

        var ids = employeeInformationIds.Distinct().ToArray();
        return await _context.Contracts
            .AsNoTracking()
            .Where(c => ids.Contains(c.EmployeeInformationId)
                     && (c.Status == ContractStatus.Active
                         || c.Status == ContractStatus.PendingTermination))
            .Select(c => c.EmployeeInformationId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetUserIdsWithActiveContractAsync(
        IReadOnlyCollection<int> userIds,
        DateTime atUtc,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return Array.Empty<int>();

        var ids = userIds.Distinct().ToArray();
        return await _context.Contracts
            .AsNoTracking()
            .Where(c => (c.Status == ContractStatus.Active
                         || c.Status == ContractStatus.PendingTermination)
                     && c.EffectiveFrom <= atUtc
                     && (c.EffectiveTo == null || c.EffectiveTo >= atUtc)
                     && c.EmployeeInformation.UserId != null
                     && ids.Contains(c.EmployeeInformation.UserId!.Value))
            .Select(c => c.EmployeeInformation.UserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public Task<Domain.Entities.Contract?> GetForDocumentGenerationAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Contracts
            .AsNoTracking()
            .Include(c => c.EmployeeInformation)
            .Include(c => c.EmployerParty)
            .Include(c => c.Position)
            .Include(c => c.SalaryGrade)
            .Include(c => c.ContractAllowances)
                .ThenInclude(a => a.AllowanceType)
            .Include(c => c.Template)
                .ThenInclude(t => t!.TemplateFile)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<int> AutoConvertExpiredDefiniteAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // Cutoff = nowUtc - 30 ngày: HĐ Definite kết thúc ≥ 30 ngày trước mà vẫn Active = phải convert.
        // (Điều 20 k.2.b BLLĐ 2019: nếu sau 30 ngày kể từ EffectiveTo mà chưa ký HĐ mới thì
        // tự động trở thành Indefinite.) — chỉ áp cho các Kind thuộc BLLĐ có khả năng auto-convert.
        var cutoff = nowUtc.AddDays(-LaborLawConstants.ContractDuration.AutoConvertToIndefiniteAfterDays);

        // Kind list precomputed từ metadata registry (SQL Server hiểu Contains via Where in-clause).
        var autoConvertKinds = ContractKindMetadata.All
            .Where(m => m.AllowsAutoConvert)
            .Select(m => m.Kind)
            .ToList();

        var candidates = await _context.Contracts
            .Where(c => c.Status == ContractStatus.Active
                && autoConvertKinds.Contains(c.Kind)
                && c.ContractCategory == ContractCategory.Definite
                && c.EffectiveTo != null
                && c.EffectiveTo.Value < cutoff
                && c.ConvertedFromDefiniteFlag == false)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return 0;

        // #57: KHÔNG nới HĐ ra vô hạn nếu NV đã ký HĐ khác tiếp quản sau khi HĐ này hết hạn —
        // nếu không sẽ sinh 2 HĐ Active chồng khoảng hiệu lực (đi vòng qua guard CONTRACT_OVERLAP,
        // vốn chỉ chặn ở ActivateAsync). Đúng tinh thần Đ.20 k.2: đã ký HĐ mới thì không gia hạn ngầm
        // ⇒ HĐ hết hạn này chỉ đơn thuần Expired, không auto-convert.
        var employeeIds = candidates.Select(c => c.EmployeeInformationId).Distinct().ToList();
        var candidateIds = candidates.Select(c => c.Id).ToHashSet();
        var otherActive = await _context.Contracts.AsNoTracking()
            .Where(c => employeeIds.Contains(c.EmployeeInformationId)
                && c.Status == ContractStatus.Active
                && c.ArchivedAt == null
                && !candidateIds.Contains(c.Id))
            .Select(c => new { c.EmployeeInformationId, c.EffectiveTo })
            .ToListAsync(cancellationToken);

        foreach (var contract in candidates)
        {
            var effectiveTo = contract.EffectiveTo!.Value;
            var hasSuccessor = otherActive.Any(o =>
                o.EmployeeInformationId == contract.EmployeeInformationId
                && (o.EffectiveTo == null || o.EffectiveTo.Value > effectiveTo));

            if (hasSuccessor)
            {
                // NV đã có HĐ Active khác phủ khoảng sau EffectiveTo ⇒ HĐ này hết hạn thật.
                contract.Status = ContractStatus.Expired;
            }
            else
            {
                contract.ContractCategory = ContractCategory.Indefinite;
                contract.EffectiveTo = null;
                contract.ConvertedFromDefiniteFlag = true;
            }
            contract.UpdatedAt = nowUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return candidates.Count;
    }

    public async Task<(int ExpiredCount, IReadOnlyList<int> AffectedEmployeeInformationIds)> ExpireOverdueContractsAsync(
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // 2 nhóm HĐ với logic expire khác nhau theo ContractKindMetadata.AllowsAutoConvert:
        //  A) AllowsAutoConvert = true (hiện chỉ LaborFullTime): giữ "vùng đệm 30 ngày" theo
        //     Đ.20 k.2 BLLĐ — nếu có HĐ con (đã ký tiếp) thì expire ngay; nếu chưa có HĐ con
        //     thì để AutoConvertJob chuyển sang Indefinite sau 30 ngày.
        //  B) AllowsAutoConvert = false (Probation, LaborPartTime, VocationalTraining,
        //     PieceWork, Service, Collaborator): hết EffectiveTo là chấm dứt luôn — không có
        //     khái niệm gia hạn ngầm hay auto-convert cho các Kind này.
        var candidates = await _context.Contracts
            .Where(c => c.Status == ContractStatus.Active
                && c.EffectiveTo != null
                && c.EffectiveTo.Value < nowUtc)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return (0, Array.Empty<int>());

        var autoConvertKinds = ContractKindMetadata.All
            .Where(m => m.AllowsAutoConvert)
            .Select(m => m.Kind)
            .ToHashSet();

        var groupA = candidates.Where(c => autoConvertKinds.Contains(c.Kind)).ToList();
        var groupB = candidates.Where(c => !autoConvertKinds.Contains(c.Kind)).ToList();

        // Nhóm A: chỉ expire khi có HĐ con.
        List<Contract> toExpireA;
        if (groupA.Count == 0)
        {
            toExpireA = new List<Contract>();
        }
        else
        {
            var groupAIds = groupA.Select(c => c.Id).ToHashSet();
            var hasChild = await _context.Contracts
                .AsNoTracking()
                .Where(c => c.ParentContractId.HasValue && groupAIds.Contains(c.ParentContractId.Value))
                .Select(c => c.ParentContractId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            var hasChildSet = hasChild.ToHashSet();
            toExpireA = groupA.Where(c => hasChildSet.Contains(c.Id)).ToList();
        }

        // Nhóm B: expire toàn bộ.
        var toExpire = toExpireA.Concat(groupB).ToList();

        foreach (var contract in toExpire)
        {
            contract.Status = ContractStatus.Expired;
            contract.UpdatedAt = nowUtc;
        }

        if (toExpire.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        var affectedEmployeeIds = toExpire
            .Select(c => c.EmployeeInformationId)
            .Distinct()
            .ToList();
        return (toExpire.Count, affectedEmployeeIds);
    }

    public async Task<int> ArchiveOldContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var cutoff = nowUtc.AddYears(-LaborLawConstants.Retention.ContractRetentionYearsAfterTermination);

        // (a) Terminated ≥ 10y: dựa vào ContractTermination.TerminationDate.
        var terminatedCandidates = await _context.Contracts
            .Where(c => c.ArchivedAt == null
                && c.Status == ContractStatus.Terminated
                && c.ContractTermination != null
                && c.ContractTermination.TerminationDate <= cutoff)
            .ToListAsync(cancellationToken);

        // (b) Expired ≥ 10y: dựa vào EffectiveTo.
        var expiredCandidates = await _context.Contracts
            .Where(c => c.ArchivedAt == null
                && c.Status == ContractStatus.Expired
                && c.EffectiveTo != null
                && c.EffectiveTo.Value <= cutoff)
            .ToListAsync(cancellationToken);

        var all = terminatedCandidates.Concat(expiredCandidates).DistinctBy(c => c.Id).ToList();
        foreach (var contract in all)
        {
            contract.ArchivedAt = nowUtc;
            contract.UpdatedAt = nowUtc;
        }

        if (all.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
        return all.Count;
    }

    /// <summary>
    /// Chép trường vô hướng từ bản DETACHED (handler đã dựng bằng <c>ContractBuilder.ApplyUpdate</c>)
    /// sang entity đang được EF theo dõi.
    ///
    /// <para>⚠️ Danh sách này là VIẾT TAY. Quên một trường mà <c>UpdateContractCommand</c> nhận vào
    /// thì thay đổi bị nuốt IM LẶNG — API vẫn trả 200 "đã cập nhật" còn DB giữ nguyên giá trị cũ,
    /// không lỗi, không log. Đã dính đúng kiểu đó với <c>Kind</c> / <c>TaxPolicy</c> /
    /// <c>UnionFundExemptFlag</c>: HR tick "Miễn trích KPCĐ" rồi lưu, mở lại vẫn thấy "DN trích".
    /// Thêm trường mới vào lệnh sửa thì PHẢI thêm vào đây (test
    /// <c>UpdateDraftPersistsAllEditableFieldsTests</c> canh chừng).</para>
    ///
    /// <para>Cố ý KHÔNG chép: <c>Id</c>, <c>Status</c>, <c>ConvertedFromDefiniteFlag</c>,
    /// <c>SignedAt</c>/<c>SignedPlace</c>, <c>ArchivedAt</c>, <c>ProbationResultNotifiedAt</c>,
    /// <c>ProbationCancelledByParty</c> và khối Created* — lệnh sửa không nhận chúng, đó là trạng
    /// thái do các luồng khác (kích hoạt, ký, chấm dứt, job) làm chủ.</para>
    /// </summary>
    private static void ApplyScalarFields(Domain.Entities.Contract tracked, Domain.Entities.Contract source)
    {
        tracked.ContractNo = source.ContractNo;
        tracked.Kind = source.Kind;
        tracked.ContractCategory = source.ContractCategory;
        tracked.ContractSubtype = source.ContractSubtype;
        tracked.Form = source.Form;
        tracked.EmployeeInformationId = source.EmployeeInformationId;
        tracked.EmployerPartyId = source.EmployerPartyId;
        tracked.EmployerSignerUserId = source.EmployerSignerUserId;
        tracked.PositionId = source.PositionId;
        tracked.JobDescription = source.JobDescription;
        tracked.WorkLocation = source.WorkLocation;
        tracked.EffectiveFrom = source.EffectiveFrom;
        tracked.EffectiveTo = source.EffectiveTo;
        tracked.SalaryGradeId = source.SalaryGradeId;
        tracked.SalaryAmountSnapshot = source.SalaryAmountSnapshot;
        tracked.SalaryCurrencySnapshot = source.SalaryCurrencySnapshot;
        tracked.SalaryFormType = source.SalaryFormType;
        tracked.SalaryPayCycle = source.SalaryPayCycle;
        tracked.PromotionPolicy = source.PromotionPolicy;
        tracked.WorkingTimeJson = source.WorkingTimeJson;
        tracked.PpePolicy = source.PpePolicy;
        tracked.SocialInsuranceFlag = source.SocialInsuranceFlag;
        tracked.HealthInsuranceFlag = source.HealthInsuranceFlag;
        tracked.UnemploymentInsuranceFlag = source.UnemploymentInsuranceFlag;
        tracked.UnionFundExemptFlag = source.UnionFundExemptFlag;
        tracked.TaxPolicy = source.TaxPolicy;
        tracked.TrainingPolicy = source.TrainingPolicy;
        tracked.ConfidentialityClause = source.ConfidentialityClause;
        tracked.TemplateId = source.TemplateId;
        tracked.ParentContractId = source.ParentContractId;
        tracked.HasProbation = source.HasProbation;
        tracked.ProbationStartDate = source.ProbationStartDate;
        tracked.ProbationEndDate = source.ProbationEndDate;
        tracked.ProbationSalary = source.ProbationSalary;
        tracked.ProbationResultStatus = source.ProbationResultStatus;
        tracked.UpdatedAt = source.UpdatedAt;
    }
}
