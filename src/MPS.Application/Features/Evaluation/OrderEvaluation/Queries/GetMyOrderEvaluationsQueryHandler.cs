using AutoMapper;
using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Evaluation;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Evaluation;

namespace MPS.Application.Features.Evaluation.OrderEvaluation.Queries;

public class GetMyOrderEvaluationsQueryHandler
    : IRequestHandler<GetMyOrderEvaluationsQuery, IReadOnlyList<OrderEvaluationSummaryDto>>
{
    private readonly IOrderEvaluationRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IMapper _mapper;

    public GetMyOrderEvaluationsQueryHandler(
        IOrderEvaluationRepository repo, ICurrentUserService currentUser, IMapper mapper)
    {
        _repo = repo;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<OrderEvaluationSummaryDto>> Handle(
        GetMyOrderEvaluationsQuery request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        // Endpoint khai cả OrderEvaluationReadAll lẫn ReadOwn nhưng trước đây LUÔN lọc theo evaluator
        // ⇒ Admin/HR/GĐ (ReadAll) luôn nhận [] và tưởng cơ chế sinh bảng-theo-đơn hỏng. Nay: có ReadAll
        // thì xem mọi bảng, không có thì vẫn chỉ xem bảng mình phụ trách.
        var evaluatorFilter = _currentUser.HasPermission(Permissions.OrderEvaluationReadAll)
            ? (int?)null
            : userId;

        var items = await _repo.GetForEvaluatorAsync(evaluatorFilter, request.Status, request.DepartmentId, ct);
        return _mapper.Map<IReadOnlyList<OrderEvaluationSummaryDto>>(items);
    }
}
