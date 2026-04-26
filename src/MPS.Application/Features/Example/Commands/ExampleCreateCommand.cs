using MediatR;

namespace MPS.Application.Features.Example.Commands;

/// <summary>
/// Ví dụ Command (CQRS) - đại diện cho một hành động ghi dữ liệu.
/// </summary>
public record ExampleCreateCommand(string Name) : IRequest<Guid>;
