using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Notifications;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Employee;
using MPS.Domain.Enums.Notifications;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Employee;
using RequestEntity = MPS.Domain.Entities.Employee.DependentRegistrationRequest;

namespace MPS.Application.Features.Employee.Dependent.Commands;

public class CreateDependentRegistrationRequestCommandHandler : IRequestHandler<CreateDependentRegistrationRequestCommand, int>
{
    private readonly IDependentRegistrationRequestRepository _repository;
    private readonly IEmployeeDependentRepository _dependentRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly IPublisher _publisher;
    private readonly IAttendanceApproverResolver _approverResolver;

    public CreateDependentRegistrationRequestCommandHandler(
        IDependentRegistrationRequestRepository repository,
        IEmployeeDependentRepository dependentRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock,
        IPublisher publisher,
        IAttendanceApproverResolver approverResolver)
    {
        _repository = repository;
        _dependentRepository = dependentRepository;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
        _publisher = publisher;
        _approverResolver = approverResolver;
    }

    public async Task<int> Handle(CreateDependentRegistrationRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        switch (request.RequestType)
        {
            case DependentRequestType.Register:
                EnsureRegisterPayload(request);
                await EnsureCitizenIdNotClaimedAsync(request.CitizenId, employeeId, cancellationToken);
                break;

            case DependentRequestType.Update:
            case DependentRequestType.Deactivate:
                await EnsureTargetOwnershipAsync(request, employeeId, cancellationToken);
                break;
        }

        var entity = new RequestEntity
        {
            EmployeeInformationId = employeeId,
            RequestType = request.RequestType,
            TargetDependentId = request.TargetDependentId,
            FullName = request.FullName?.Trim() ?? string.Empty,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender?.Trim(),
            Relationship = request.Relationship,
            TaxIdentificationNumber = request.TaxIdentificationNumber?.Trim(),
            CitizenId = request.CitizenId?.Trim(),
            RegisteredFromDate = request.RegisteredFromDate,
            RegisteredToDate = request.RegisteredToDate,
            SupportingDocumentUrl = request.SupportingDocumentUrl?.Trim(),
            Reason = request.Reason?.Trim(),
            Status = DependentRequestStatus.Pending,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        var requestId = await _repository.AddAsync(entity, cancellationToken);

        // Dependent: chỉ HR/SystemAdmin duyệt — resolver dùng permission DependentRequest.Approve.All.
        var approvers = await _approverResolver.ResolveForSubmitAsync(
            AttendanceRequestKind.Dependent, approverRoute: null, employeeId,
            tierOneApproverUserId: null, excludeUserId: userId, cancellationToken);

        await _publisher.Publish(new AttendanceRequestSubmittedNotification(
            Kind: AttendanceRequestKind.Dependent,
            RequestId: requestId,
            SubmittedByUserId: userId,
            RequesterUserId: userId,
            ApproverUserIds: approvers,
            Summary: $"Đơn NPT ({request.RequestType}): {entity.FullName}"),
            cancellationToken);

        return requestId;
    }

    private static void EnsureRegisterPayload(CreateDependentRegistrationRequestCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new DomainValidationException("Register cần FullName.");
        if (request.DateOfBirth is null)
            throw new DomainValidationException("Register cần DateOfBirth.");
        if (request.Relationship is null)
            throw new DomainValidationException("Register cần Relationship.");
        if (request.RegisteredFromDate is null)
            throw new DomainValidationException("Register cần RegisteredFromDate.");
        if (string.IsNullOrWhiteSpace(request.SupportingDocumentUrl))
            throw new DomainValidationException("Register cần SupportingDocumentUrl (giấy khai sinh/CCCD).");
    }

    /// <summary>
    /// Chặn khai trùng người phụ thuộc ngay từ lúc NỘP ĐƠN — kể cả khi người đó đang được nhân
    /// viên KHÁC đăng ký. Trước đây chỉ dò trong cùng một nhân viên nên hai vợ chồng cùng khai một
    /// đứa con thì cả hai đơn đều qua và giảm trừ gia cảnh bị tính hai lần.
    /// Chốt chặn thật vẫn nằm ở bước duyệt (chống race giữa hai đơn đang chờ).
    /// </summary>
    private async Task EnsureCitizenIdNotClaimedAsync(
        string? citizenId,
        int employeeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(citizenId)) return;

        var trimmed = citizenId.Trim();
        var claimed = await _dependentRepository.FindActiveByCitizenIdAsync(
            trimmed, excludeDependentId: null, cancellationToken);
        if (claimed is null) return;

        throw new DomainValidationException(
            claimed.EmployeeInformationId == employeeId
                ? DependentClaimRules.DuplicateInSameEmployee(trimmed)
                : DependentClaimRules.ClaimedByAnotherEmployee(trimmed));
    }

    private async Task EnsureTargetOwnershipAsync(
        CreateDependentRegistrationRequestCommand request,
        int employeeId,
        CancellationToken cancellationToken)
    {
        if (request.TargetDependentId is null)
            throw new DomainValidationException("Update/Deactivate cần TargetDependentId.");
        var target = await _dependentRepository.GetByIdAsync(request.TargetDependentId.Value, cancellationToken)
            ?? throw new NotFoundException($"Dependent {request.TargetDependentId} not found.");
        if (target.EmployeeInformationId != employeeId)
            throw new ForbiddenAccessException("Không được thao tác với người phụ thuộc của employee khác.");
    }
}
