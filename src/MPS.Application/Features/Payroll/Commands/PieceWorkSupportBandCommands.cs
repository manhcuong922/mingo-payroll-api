using MediatR;
using MPS.Application.DTOs.Payroll;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

// ===== Xem bậc hỗ trợ theo tiến độ (HĐ khoán việc) =====
public sealed record GetPieceWorkSupportBandsQuery(bool? IsActive)
    : IRequest<IReadOnlyList<PieceWorkSupportBandDto>>;

public sealed class GetPieceWorkSupportBandsQueryHandler
    : IRequestHandler<GetPieceWorkSupportBandsQuery, IReadOnlyList<PieceWorkSupportBandDto>>
{
    private readonly IPieceWorkSupportBandRepository _repo;
    public GetPieceWorkSupportBandsQueryHandler(IPieceWorkSupportBandRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<PieceWorkSupportBandDto>> Handle(
        GetPieceWorkSupportBandsQuery request, CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(request.IsActive, ct);
        return items
            .OrderBy(x => x.MinHours)
            .Select(x => new PieceWorkSupportBandDto(x.Id, x.MinHours, x.MaxHours, x.Amount, x.Label, x.IsActive))
            .ToList();
    }
}

// ===== Tạo bậc =====
public sealed record CreatePieceWorkSupportBandCommand(
    decimal MinHours, decimal? MaxHours, decimal Amount, string? Label) : IRequest<int>;

public sealed class CreatePieceWorkSupportBandCommandHandler
    : IRequestHandler<CreatePieceWorkSupportBandCommand, int>
{
    private readonly IPieceWorkSupportBandRepository _repo;
    private readonly IPayrollStaleMarker _payrollStale;

    public CreatePieceWorkSupportBandCommandHandler(
        IPieceWorkSupportBandRepository repo, IPayrollStaleMarker payrollStale)
    {
        _repo = repo;
        _payrollStale = payrollStale;
    }

    public async Task<int> Handle(CreatePieceWorkSupportBandCommand request, CancellationToken ct)
    {
        ValidateBand(request.MinHours, request.MaxHours, request.Amount);

        var id = await _repo.AddAsync(new PieceWorkSupportBand
        {
            MinHours = request.MinHours,
            MaxHours = request.MaxHours,
            Amount = request.Amount,
            Label = request.Label,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        }, ct);

        await _payrollStale.MarkAllStaleAsync(
            $"Thêm bậc hỗ trợ theo tiến độ (từ {request.MinHours:0.##} giờ = {request.Amount:N0}đ)", ct);
        return id;
    }

    /// <summary>
    /// Bậc phải hợp lệ về mặt số học. KHÔNG kiểm chồng lấn giữa các bậc: mốc "từ 150h" và "từ 200h"
    /// vốn chồng nhau ở biên, và <c>PieceWorkSupportResolver</c> đã xử lý bằng quy tắc "bậc có
    /// MinHours lớn nhất thắng".
    /// </summary>
    internal static void ValidateBand(decimal minHours, decimal? maxHours, decimal amount)
    {
        if (minHours < 0)
            throw new DomainValidationException("Số giờ tối thiểu không được âm.");
        if (maxHours is { } max && max < minHours)
            throw new DomainValidationException("Số giờ tối đa phải ≥ số giờ tối thiểu.");
        if (amount < 0)
            throw new DomainValidationException("Số tiền hỗ trợ không được âm.");
    }
}

// ===== Sửa bậc =====
public sealed record UpdatePieceWorkSupportBandCommand(
    int Id, decimal MinHours, decimal? MaxHours, decimal Amount, string? Label, bool IsActive) : IRequest;

public sealed class UpdatePieceWorkSupportBandCommandHandler
    : IRequestHandler<UpdatePieceWorkSupportBandCommand>
{
    private readonly IPieceWorkSupportBandRepository _repo;
    private readonly IPayrollStaleMarker _payrollStale;

    public UpdatePieceWorkSupportBandCommandHandler(
        IPieceWorkSupportBandRepository repo, IPayrollStaleMarker payrollStale)
    {
        _repo = repo;
        _payrollStale = payrollStale;
    }

    public async Task Handle(UpdatePieceWorkSupportBandCommand request, CancellationToken ct)
    {
        CreatePieceWorkSupportBandCommandHandler.ValidateBand(request.MinHours, request.MaxHours, request.Amount);

        var ok = await _repo.UpdateAsync(new PieceWorkSupportBand
        {
            Id = request.Id,
            MinHours = request.MinHours,
            MaxHours = request.MaxHours,
            Amount = request.Amount,
            Label = request.Label,
            IsActive = request.IsActive,
        }, ct);
        if (!ok) throw new NotFoundException($"Không tìm thấy bậc hỗ trợ #{request.Id}.");

        // Bậc áp cho MỌI HĐ khoán nên không khoanh theo tháng/người được: đánh cờ mọi kỳ Draft đang
        // mở, giống cách làm của band hệ số năng suất. Kỳ đã chốt giữ snapshot riêng nên không đổi.
        await _payrollStale.MarkAllStaleAsync(
            $"Sửa bậc hỗ trợ theo tiến độ #{request.Id} (từ {request.MinHours:0.##} giờ = {request.Amount:N0}đ)", ct);
    }
}

// ===== Xoá bậc =====
public sealed record DeletePieceWorkSupportBandCommand(int Id) : IRequest;

public sealed class DeletePieceWorkSupportBandCommandHandler
    : IRequestHandler<DeletePieceWorkSupportBandCommand>
{
    private readonly IPieceWorkSupportBandRepository _repo;
    private readonly IPayrollStaleMarker _payrollStale;

    public DeletePieceWorkSupportBandCommandHandler(
        IPieceWorkSupportBandRepository repo, IPayrollStaleMarker payrollStale)
    {
        _repo = repo;
        _payrollStale = payrollStale;
    }

    public async Task Handle(DeletePieceWorkSupportBandCommand request, CancellationToken ct)
    {
        var ok = await _repo.DeleteAsync(request.Id, ct);
        if (!ok) throw new NotFoundException($"Không tìm thấy bậc hỗ trợ #{request.Id}.");

        await _payrollStale.MarkAllStaleAsync($"Xoá bậc hỗ trợ theo tiến độ #{request.Id}", ct);
    }
}
