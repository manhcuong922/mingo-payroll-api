using System.Text.RegularExpressions;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Features.Payroll.Commands;

// ===== Tạo tham số công ty =====
public sealed partial record CreatePayrollParameterCommand(
    string Key, string Name, decimal Value, string? Description) : IRequest<int>
{
    [GeneratedRegex("^[A-Z][A-Z0-9_]*$")]
    internal static partial Regex KeyPattern();
}

public sealed class CreatePayrollParameterCommandHandler : IRequestHandler<CreatePayrollParameterCommand, int>
{
    private readonly IPayrollParameterRepository _repo;
    public CreatePayrollParameterCommandHandler(IPayrollParameterRepository repo) => _repo = repo;

    public async Task<int> Handle(CreatePayrollParameterCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || !CreatePayrollParameterCommand.KeyPattern().IsMatch(request.Key))
            throw new DomainValidationException("Khoá tham số phải VIẾT_HOA_GẠCH_DƯỚI (vd MEAL_PRICE_PER_WORKDAY).");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainValidationException("Tên tham số không được trống.");
        if (await _repo.KeyExistsAsync(request.Key, null, ct))
            throw new ConflictException($"Khoá tham số '{request.Key}' đã tồn tại.");

        return await _repo.AddAsync(new PayrollParameter
        {
            Key = request.Key,
            Name = request.Name.Trim(),
            Value = request.Value,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        }, ct);
    }
}

// ===== Sửa tham số (khoá bất biến) =====
public sealed record UpdatePayrollParameterCommand(
    int Id, string Name, decimal Value, string? Description, bool IsActive) : IRequest;

public sealed class UpdatePayrollParameterCommandHandler : IRequestHandler<UpdatePayrollParameterCommand>
{
    private readonly IPayrollParameterRepository _repo;
    public UpdatePayrollParameterCommandHandler(IPayrollParameterRepository repo) => _repo = repo;

    public async Task Handle(UpdatePayrollParameterCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new DomainValidationException("Tên tham số không được trống.");

        var ok = await _repo.UpdateAsync(new PayrollParameter
        {
            Id = request.Id,
            Name = request.Name.Trim(),
            Value = request.Value,
            Description = request.Description,
            IsActive = request.IsActive,
        }, ct);
        if (!ok) throw new NotFoundException($"Không tìm thấy tham số #{request.Id}.");
    }
}
