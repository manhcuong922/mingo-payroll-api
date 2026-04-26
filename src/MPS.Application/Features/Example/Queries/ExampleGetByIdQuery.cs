using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Example.Queries;

/// <summary>
/// Ví dụ Query (CQRS) - đại diện cho một yêu cầu đọc dữ liệu.
/// </summary>
public record ExampleGetByIdQuery(Guid Id) : IRequest<ExampleDto?>;
