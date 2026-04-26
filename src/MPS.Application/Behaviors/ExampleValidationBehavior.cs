using MediatR;

namespace MPS.Application.Behaviors;

/// <summary>
/// Ví dụ Pipeline Behavior - xử lý cross-cutting concern (validation, logging...).
/// </summary>
public class ExampleValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // TODO: Add validation logic before handler
        return await next();
    }
}
