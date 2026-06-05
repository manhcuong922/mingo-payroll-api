using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Behaviors;

/// <summary>
/// Pipeline behavior — chặn mọi write command trên HĐ đã archived.
/// Chỉ kích hoạt khi request implement <see cref="IContractScopedCommand"/>.
/// Throw <see cref="ContractArchivedException"/> → middleware map sang 409 + code
/// CONTRACT_ARCHIVED. Đặt TRƯỚC ValidationBehavior trong pipeline để fail-fast.
/// </summary>
public class ArchivedContractGuardBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IContractRepository _contracts;

    public ArchivedContractGuardBehavior(IContractRepository contracts)
    {
        _contracts = contracts;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IContractScopedCommand scoped && scoped.ContractId > 0)
        {
            var archivedAt = await _contracts.GetArchivedAtAsync(scoped.ContractId, cancellationToken);
            if (archivedAt is not null)
                throw new ContractArchivedException(scoped.ContractId);
        }

        return await next();
    }
}
