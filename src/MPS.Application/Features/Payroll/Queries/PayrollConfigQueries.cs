using MediatR;
using MPS.Application.DTOs.Payroll;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Queries;

// ===== Bảng công thức: danh sách =====
public sealed record GetFormulaSetsQuery(bool? IsActive) : IRequest<IReadOnlyList<FormulaSetDto>>;

public sealed class GetFormulaSetsQueryHandler
    : IRequestHandler<GetFormulaSetsQuery, IReadOnlyList<FormulaSetDto>>
{
    private readonly IPayrollFormulaSetRepository _repo;
    public GetFormulaSetsQueryHandler(IPayrollFormulaSetRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<FormulaSetDto>> Handle(GetFormulaSetsQuery request, CancellationToken ct)
    {
        var sets = await _repo.GetAllAsync(request.IsActive, ct);
        var result = new List<FormulaSetDto>(sets.Count);
        foreach (var s in sets)
        {
            var cols = await _repo.GetColumnsAsync(s.Id, ct);
            result.Add(new FormulaSetDto(
                s.Id, s.Name, s.Description, s.IsActive, s.IsDefault, cols.Count, s.CreatedAt, s.UpdatedAt));
        }
        return result;
    }
}

// ===== Bảng công thức: chi tiết + cột =====
public sealed record GetFormulaSetByIdQuery(int Id) : IRequest<FormulaSetDetailDto>;

public sealed class GetFormulaSetByIdQueryHandler
    : IRequestHandler<GetFormulaSetByIdQuery, FormulaSetDetailDto>
{
    private readonly IPayrollFormulaSetRepository _repo;
    public GetFormulaSetByIdQueryHandler(IPayrollFormulaSetRepository repo) => _repo = repo;

    public async Task<FormulaSetDetailDto> Handle(GetFormulaSetByIdQuery request, CancellationToken ct)
    {
        var set = await _repo.GetByIdAsync(request.Id, includeColumns: true, ct)
            ?? throw new NotFoundException($"Không tìm thấy bảng công thức #{request.Id}.");
        var columns = set.Columns
            .OrderBy(c => c.DisplayOrder)
            .Select(PayrollConfigMapper.ToDto)
            .ToList();
        return new FormulaSetDetailDto(
            set.Id, set.Name, set.Description, set.IsActive, set.IsDefault,
            set.CreatedAt, set.UpdatedAt, columns);
    }
}

// ===== Hệ số năng suất =====
public sealed record GetProductivityCoefficientsQuery(bool? IsActive)
    : IRequest<IReadOnlyList<ProductivityCoefficientDto>>;

public sealed class GetProductivityCoefficientsQueryHandler
    : IRequestHandler<GetProductivityCoefficientsQuery, IReadOnlyList<ProductivityCoefficientDto>>
{
    private readonly IProductivityCoefficientRepository _repo;
    public GetProductivityCoefficientsQueryHandler(IProductivityCoefficientRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ProductivityCoefficientDto>> Handle(
        GetProductivityCoefficientsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.IsActive, ct);
        return items
            .OrderByDescending(x => x.MinScore)
            .Select(x => new ProductivityCoefficientDto(
                x.Id, x.MinScore, x.MaxScore, x.Coefficient, x.Label, x.IsActive))
            .ToList();
    }
}
