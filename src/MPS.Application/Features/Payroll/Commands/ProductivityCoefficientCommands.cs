using MediatR;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

// ===== Tạo band hệ số năng suất =====
public sealed record CreateProductivityCoefficientCommand(
    decimal MinScore, decimal MaxScore, decimal Coefficient, string? Label) : IRequest<int>;

public sealed class CreateProductivityCoefficientCommandHandler
    : IRequestHandler<CreateProductivityCoefficientCommand, int>
{
    private readonly IProductivityCoefficientRepository _repo;
    public CreateProductivityCoefficientCommandHandler(IProductivityCoefficientRepository repo) => _repo = repo;

    public async Task<int> Handle(CreateProductivityCoefficientCommand request, CancellationToken ct)
    {
        ValidateBand(request.MinScore, request.MaxScore, request.Coefficient);
        return await _repo.AddAsync(new ProductivityCoefficient
        {
            MinScore = request.MinScore,
            MaxScore = request.MaxScore,
            Coefficient = request.Coefficient,
            Label = request.Label,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        }, ct);
    }

    internal static void ValidateBand(decimal min, decimal max, decimal coefficient)
    {
        if (min < 0 || max < 0)
            throw new DomainValidationException("Điểm không được âm.");
        if (min > max)
            throw new DomainValidationException("Điểm tối thiểu phải ≤ điểm tối đa.");
        if (coefficient < 0)
            throw new DomainValidationException("Hệ số không được âm.");
    }
}

// ===== Sửa band hệ số năng suất =====
public sealed record UpdateProductivityCoefficientCommand(
    int Id, decimal MinScore, decimal MaxScore, decimal Coefficient, string? Label, bool IsActive) : IRequest;

public sealed class UpdateProductivityCoefficientCommandHandler
    : IRequestHandler<UpdateProductivityCoefficientCommand>
{
    private readonly IProductivityCoefficientRepository _repo;
    private readonly IPayrollStaleMarker _payrollStale;

    public UpdateProductivityCoefficientCommandHandler(
        IProductivityCoefficientRepository repo, IPayrollStaleMarker payrollStale)
    {
        _repo = repo;
        _payrollStale = payrollStale;
    }

    public async Task Handle(UpdateProductivityCoefficientCommand request, CancellationToken ct)
    {
        CreateProductivityCoefficientCommandHandler.ValidateBand(request.MinScore, request.MaxScore, request.Coefficient);
        var ok = await _repo.UpdateAsync(new ProductivityCoefficient
        {
            Id = request.Id,
            MinScore = request.MinScore,
            MaxScore = request.MaxScore,
            Coefficient = request.Coefficient,
            Label = request.Label,
            IsActive = request.IsActive,
        }, ct);
        if (!ok) throw new NotFoundException($"Không tìm thấy band hệ số #{request.Id}.");

        // #66/[D27] — band hệ số map điểm đánh giá → hệ số nhân lương gốc, áp cho TOÀN BỘ NV nên
        // không khoanh theo tháng/người được: đánh cờ mọi kỳ Draft đang mở. MarkAllStaleAsync tự
        // nuốt lỗi (side-effect phụ, không làm hỏng việc sửa band).
        await _payrollStale.MarkAllStaleAsync(
            $"Sửa band hệ số năng suất #{request.Id} (hệ số {request.Coefficient})", ct);
    }
}

// ===== Xoá band hệ số năng suất =====
public sealed record DeleteProductivityCoefficientCommand(int Id) : IRequest;

public sealed class DeleteProductivityCoefficientCommandHandler
    : IRequestHandler<DeleteProductivityCoefficientCommand>
{
    private readonly IProductivityCoefficientRepository _repo;
    public DeleteProductivityCoefficientCommandHandler(IProductivityCoefficientRepository repo) => _repo = repo;

    public async Task Handle(DeleteProductivityCoefficientCommand request, CancellationToken ct)
    {
        var ok = await _repo.DeleteAsync(request.Id, ct);
        if (!ok) throw new NotFoundException($"Không tìm thấy band hệ số #{request.Id}.");
    }
}
