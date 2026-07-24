using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Employee;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Employee;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Employee;

public class DependentRegistrationRequestRepository : IDependentRegistrationRequestRepository
{
    private readonly AppDbContext _context;

    public DependentRegistrationRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<DependentRegistrationRequest> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
        DependentRequestStatus? status,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DependentRegistrationRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.TargetDependent)
            .AsQueryable();

        if (employeeInformationId.HasValue)
            query = query.Where(r => r.EmployeeInformationId == employeeInformationId.Value);
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<DependentRegistrationRequest?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.DependentRegistrationRequests
            .AsNoTracking()
            .Include(r => r.EmployeeInformation)
            .Include(r => r.TargetDependent)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<int> AddAsync(DependentRegistrationRequest entity, CancellationToken cancellationToken = default)
    {
        await _context.DependentRegistrationRequests.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(DependentRegistrationRequest entity, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.DependentRegistrationRequests
            .FirstOrDefaultAsync(r => r.Id == entity.Id, cancellationToken);
        if (tracked is null) return false;

        tracked.RequestType = entity.RequestType;
        tracked.TargetDependentId = entity.TargetDependentId;
        tracked.FullName = entity.FullName;
        tracked.DateOfBirth = entity.DateOfBirth;
        tracked.Gender = entity.Gender;
        tracked.Relationship = entity.Relationship;
        tracked.TaxIdentificationNumber = entity.TaxIdentificationNumber;
        tracked.CitizenId = entity.CitizenId;
        tracked.RegisteredFromDate = entity.RegisteredFromDate;
        tracked.RegisteredToDate = entity.RegisteredToDate;
        tracked.SupportingDocumentUrl = entity.SupportingDocumentUrl;
        tracked.Status = entity.Status;
        tracked.Reason = entity.Reason;
        tracked.ReviewNote = entity.ReviewNote;
        tracked.ReviewedByUserId = entity.ReviewedByUserId;
        tracked.ReviewedAt = entity.ReviewedAt;
        tracked.CancelledAt = entity.CancelledAt;
        tracked.CancelledByUserId = entity.CancelledByUserId;
        tracked.CancelReason = entity.CancelReason;
        tracked.UpdatedAt = entity.UpdatedAt;
        tracked.UpdatedByUserId = entity.UpdatedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ApproveAndApplyAsync(
        int requestId,
        int reviewedByUserId,
        string? reviewNote,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var request = await _context.DependentRegistrationRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        if (request.Status != DependentRequestStatus.Pending)
            throw new DomainValidationException(
                $"Dependent request {requestId} cannot be approved — current status is {request.Status}.");

        if (request.CreatedByUserId == reviewedByUserId)
            throw new DomainValidationException("BR-10: Self-approval is forbidden.");

        switch (request.RequestType)
        {
            case DependentRequestType.Register:
                await CreateDependentAsync(request, reviewedByUserId, nowUtc, cancellationToken);
                break;

            case DependentRequestType.Update:
                await UpdateDependentAsync(request, reviewedByUserId, nowUtc, cancellationToken);
                break;

            case DependentRequestType.Deactivate:
                await DeactivateDependentAsync(request, reviewedByUserId, nowUtc, cancellationToken);
                break;
        }

        // Đẩy thay đổi của bước trên (thêm mới / hạ IsActive) xuống DB TRƯỚC khi đếm.
        //
        // ⚠️ Thiếu dòng này là lệch 1 người: bản ghi vừa `AddAsync` chưa nằm trong DB lúc COUNT nên
        // duyệt đăng ký cho ra `DependentsNo = active − 1`, còn duyệt hủy lại cho `active + 1`
        // (hủy một người phụ thuộc mà số lại TĂNG). Đây là số hiển thị trên hồ sơ và là nguồn dự
        // phòng cho giảm trừ gia cảnh, nên sai ở đây là sai tiền thuế.
        await _context.SaveChangesAsync(cancellationToken);

        // Sync EmployeeInformation.DependentsNo với số active dependents.
        var employee = await _context.EmployeeInformations
            .FirstOrDefaultAsync(e => e.Id == request.EmployeeInformationId, cancellationToken)
            ?? throw new NotFoundException($"Employee {request.EmployeeInformationId} not found.");
        var activeCount = await _context.EmployeeDependents
            .Where(d => d.EmployeeInformationId == request.EmployeeInformationId && d.IsActive)
            .CountAsync(cancellationToken);
        employee.DependentsNo = activeCount;

        request.Status = DependentRequestStatus.Approved;
        request.ReviewedByUserId = reviewedByUserId;
        request.ReviewedAt = nowUtc;
        request.ReviewNote = reviewNote;
        request.UpdatedAt = nowUtc;
        request.UpdatedByUserId = reviewedByUserId;

        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    private async Task CreateDependentAsync(
        DependentRegistrationRequest request,
        int userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (request.DateOfBirth is null || request.Relationship is null || request.RegisteredFromDate is null)
            throw new DomainValidationException("Register cần DateOfBirth, Relationship, RegisteredFromDate.");
        if (string.IsNullOrWhiteSpace(request.SupportingDocumentUrl))
            throw new DomainValidationException("Register cần SupportingDocumentUrl (giấy khai sinh/CCCD).");

        await EnsureCitizenIdNotClaimedAsync(
            request.CitizenId, request.EmployeeInformationId, excludeDependentId: null, cancellationToken);

        var dependent = new EmployeeDependent
        {
            EmployeeInformationId = request.EmployeeInformationId,
            FullName = request.FullName,
            DateOfBirth = request.DateOfBirth.Value,
            Gender = request.Gender,
            Relationship = request.Relationship.Value,
            TaxIdentificationNumber = request.TaxIdentificationNumber,
            CitizenId = request.CitizenId,
            RegisteredFromDate = request.RegisteredFromDate.Value,
            RegisteredToDate = request.RegisteredToDate,
            IsActive = true,
            CreatedAt = nowUtc,
            CreatedByUserId = userId,
        };
        await _context.EmployeeDependents.AddAsync(dependent, cancellationToken);
    }

    private async Task UpdateDependentAsync(
        DependentRegistrationRequest request,
        int userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (request.TargetDependentId is null)
            throw new DomainValidationException("Update cần TargetDependentId.");

        var dependent = await _context.EmployeeDependents
            .FirstOrDefaultAsync(d => d.Id == request.TargetDependentId.Value, cancellationToken)
            ?? throw new NotFoundException($"Dependent {request.TargetDependentId} not found.");

        if (dependent.EmployeeInformationId != request.EmployeeInformationId)
            throw new DomainValidationException("TargetDependent không thuộc employee này.");

        if (!string.IsNullOrWhiteSpace(request.FullName))
            dependent.FullName = request.FullName;
        if (request.DateOfBirth.HasValue)
            dependent.DateOfBirth = request.DateOfBirth.Value;
        if (request.Gender is not null)
            dependent.Gender = request.Gender;
        if (request.Relationship.HasValue)
            dependent.Relationship = request.Relationship.Value;
        if (request.TaxIdentificationNumber is not null)
            dependent.TaxIdentificationNumber = request.TaxIdentificationNumber;
        if (request.CitizenId is not null && request.CitizenId != dependent.CitizenId)
        {
            // Đổi CCCD qua phụ lục cũng phải qua cùng một cửa: nếu không, đây là đường vòng để
            // khai trùng người phụ thuộc mà không bị guard ở nhánh đăng ký mới.
            await EnsureCitizenIdNotClaimedAsync(
                request.CitizenId, request.EmployeeInformationId, dependent.Id, cancellationToken);
            dependent.CitizenId = request.CitizenId;
        }
        if (request.RegisteredFromDate.HasValue)
            dependent.RegisteredFromDate = request.RegisteredFromDate.Value;
        if (request.RegisteredToDate.HasValue)
            dependent.RegisteredToDate = request.RegisteredToDate;

        dependent.UpdatedAt = nowUtc;
        dependent.UpdatedByUserId = userId;
    }

    /// <summary>
    /// Chốt chặn "mỗi người phụ thuộc chỉ một người lao động giảm trừ" ở bước DUYỆT.
    ///
    /// Phải kiểm lại ở đây chứ không chỉ lúc nộp đơn: hai nhân viên có thể nộp hai đơn cho cùng một
    /// người khi chưa đơn nào được duyệt — lúc nộp cả hai đều "sạch", chỉ tới khi duyệt mới lộ
    /// trùng. Trước đây guard chỉ dò trong CÙNG một nhân viên nên cả hai đơn đều được duyệt 204 và
    /// giảm trừ gia cảnh bị tính hai lần (lỗi #38).
    /// </summary>
    private async Task EnsureCitizenIdNotClaimedAsync(
        string? citizenId,
        int employeeInformationId,
        int? excludeDependentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(citizenId)) return;

        var claimed = await _context.EmployeeDependents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.IsActive
                    && d.CitizenId == citizenId
                    && (excludeDependentId == null || d.Id != excludeDependentId),
                cancellationToken);
        if (claimed is null) return;

        throw new DomainValidationException(
            claimed.EmployeeInformationId == employeeInformationId
                ? DependentClaimRules.DuplicateInSameEmployee(citizenId)
                : DependentClaimRules.ClaimedByAnotherEmployee(citizenId));
    }

    private async Task DeactivateDependentAsync(
        DependentRegistrationRequest request,
        int userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (request.TargetDependentId is null)
            throw new DomainValidationException("Deactivate cần TargetDependentId.");

        var dependent = await _context.EmployeeDependents
            .FirstOrDefaultAsync(d => d.Id == request.TargetDependentId.Value, cancellationToken)
            ?? throw new NotFoundException($"Dependent {request.TargetDependentId} not found.");

        if (dependent.EmployeeInformationId != request.EmployeeInformationId)
            throw new DomainValidationException("TargetDependent không thuộc employee này.");

        dependent.IsActive = false;
        if (request.RegisteredToDate.HasValue)
            dependent.RegisteredToDate = request.RegisteredToDate;
        else
            dependent.RegisteredToDate ??= DateOnly.FromDateTime(nowUtc.AddHours(7));
        dependent.UpdatedAt = nowUtc;
        dependent.UpdatedByUserId = userId;
    }
}
