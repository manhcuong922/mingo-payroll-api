using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

// ===== Tạo bảng công thức rỗng (metadata) =====
public sealed record CreateFormulaSetCommand(string Name, string? Description) : IRequest<int>;

public sealed class CreateFormulaSetCommandHandler : IRequestHandler<CreateFormulaSetCommand, int>
{
    private readonly IPayrollFormulaSetRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public CreateFormulaSetCommandHandler(IPayrollFormulaSetRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CreateFormulaSetCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainValidationException("Tên bảng công thức không được trống.");

        return await _repo.AddAsync(new PayrollFormulaSet
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            IsActive = true,
            IsDefault = false,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
        }, ct);
    }
}

// ===== Sửa metadata bảng công thức =====
public sealed record UpdateFormulaSetCommand(int Id, string Name, string? Description, bool IsActive) : IRequest;

public sealed class UpdateFormulaSetCommandHandler : IRequestHandler<UpdateFormulaSetCommand>
{
    private readonly IPayrollFormulaSetRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public UpdateFormulaSetCommandHandler(IPayrollFormulaSetRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateFormulaSetCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainValidationException("Tên bảng công thức không được trống.");

        var existing = await _repo.GetByIdAsync(request.Id, includeColumns: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng công thức #{request.Id}.");

        var ok = await _repo.UpdateAsync(new PayrollFormulaSet
        {
            Id = existing.Id,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsActive = request.IsActive,
            UpdatedByUserId = userId,
        }, ct);
        if (!ok) throw new NotFoundException($"Không tìm thấy bảng công thức #{request.Id}.");
    }
}

// ===== Đặt làm bảng mặc định =====
public sealed record SetDefaultFormulaSetCommand(int Id) : IRequest;

public sealed class SetDefaultFormulaSetCommandHandler : IRequestHandler<SetDefaultFormulaSetCommand>
{
    private readonly IPayrollFormulaSetRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public SetDefaultFormulaSetCommandHandler(IPayrollFormulaSetRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task Handle(SetDefaultFormulaSetCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        var ok = await _repo.SetDefaultAsync(request.Id, userId, ct);
        if (!ok) throw new NotFoundException($"Không tìm thấy bảng công thức #{request.Id}.");
    }
}

// ===== Xoá bảng công thức (chặn nếu đang dùng / là mặc định) =====
public sealed record DeleteFormulaSetCommand(int Id) : IRequest;

public sealed class DeleteFormulaSetCommandHandler : IRequestHandler<DeleteFormulaSetCommand>
{
    private readonly IPayrollFormulaSetRepository _repo;
    public DeleteFormulaSetCommandHandler(IPayrollFormulaSetRepository repo) => _repo = repo;

    public async Task Handle(DeleteFormulaSetCommand request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(request.Id, includeColumns: false, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng công thức #{request.Id}.");
        if (existing.IsDefault)
            throw new ConflictException("Không thể xoá bảng công thức mặc định — hãy đặt bảng khác làm mặc định trước.");
        if (await _repo.IsUsedByAnyPeriodAsync(request.Id, ct))
            throw new ConflictException("Bảng công thức đã được dùng cho ít nhất 1 kỳ lương — không thể xoá.");

        await _repo.DeleteAsync(request.Id, ct);
    }
}

// ===== Nhân bản (clone) bảng công thức + toàn bộ cột =====
public sealed record CloneFormulaSetCommand(int SourceId, string NewName, string? Description) : IRequest<int>;

public sealed class CloneFormulaSetCommandHandler : IRequestHandler<CloneFormulaSetCommand, int>
{
    private readonly IPayrollFormulaSetRepository _repo;
    private readonly IPayrollParameterRepository _parameters;
    private readonly IPayrollFormulaValidator _validator;
    private readonly ICurrentUserService _currentUser;

    public CloneFormulaSetCommandHandler(
        IPayrollFormulaSetRepository repo, IPayrollParameterRepository parameters,
        IPayrollFormulaValidator validator, ICurrentUserService currentUser)
    {
        _repo = repo;
        _parameters = parameters;
        _validator = validator;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CloneFormulaSetCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException();
        if (string.IsNullOrWhiteSpace(request.NewName))
            throw new DomainValidationException("Tên bảng công thức mới không được trống.");

        var source = await _repo.GetByIdAsync(request.SourceId, includeColumns: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng công thức nguồn #{request.SourceId}.");

        // Cột clone thuộc quyền user (IsSystem=false) → cho phép sửa/xoá tự do.
        var now = DateTime.UtcNow;
        var clonedColumns = source.Columns
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new SalaryColumnDefinition
            {
                ColumnCode = c.ColumnCode,
                Label = c.Label,
                GroupName = c.GroupName,
                DisplayOrder = c.DisplayOrder,
                DataType = c.DataType,
                ValueSource = c.ValueSource,
                LookupKey = c.LookupKey,
                FormulaJson = c.FormulaJson,
                Rounding = c.Rounding,
                IsSystem = false,
                IsMoney = c.IsMoney,
                IsEditable = c.IsEditable,
                CreatedAt = now,
            })
            .ToList();

        var paramKeys = (await _parameters.GetAllAsync(null, ct)).Select(p => p.Key).ToList();
        _validator.EnsureValidOrThrow(clonedColumns, paramKeys);

        return await _repo.AddAsync(new PayrollFormulaSet
        {
            Name = request.NewName.Trim(),
            Description = request.Description ?? $"Nhân bản từ '{source.Name}'.",
            IsActive = true,
            IsDefault = false,
            CreatedByUserId = userId,
            CreatedAt = now,
            Columns = clonedColumns,
        }, ct);
    }
}
