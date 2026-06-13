using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Utilities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserCreateCommandHandler : IRequestHandler<UserCreateCommand, UserCreateResult>
{
    private const int DefaultPasswordLength = 8;
    private const int MaxEmployeeCodeRetries = 10;

    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmployeeInformationRepository _eiRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IEmailSender _emailSender;

    public UserCreateCommandHandler(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IEmployeeInformationRepository eiRepository,
        IContractRepository contractRepository,
        IEmailSender emailSender)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _eiRepository = eiRepository;
        _contractRepository = contractRepository;
        _emailSender = emailSender;
    }

    public async Task<UserCreateResult> Handle(UserCreateCommand request, CancellationToken cancellationToken)
    {
        var ei = await _eiRepository.GetByIdAsync(request.EmployeeInformationId, cancellationToken)
            ?? throw new DomainValidationException(
                $"EMPLOYEE_INFO_NOT_FOUND: EmployeeInformation {request.EmployeeInformationId} does not exist.");

        if (ei.UserId.HasValue)
            throw new DomainValidationException("EMPLOYEE_ALREADY_PROMOTED: This employee already has a user account.");

        if (string.IsNullOrWhiteSpace(ei.Email))
            throw new DomainValidationException("EMPLOYEE_EMAIL_REQUIRED: Employee must have an email before granting an account.");

        var hasActive = await _contractRepository.HasActiveContractAsync(ei.Id, cancellationToken);
        if (!hasActive)
            throw new DomainValidationException("CONTRACT_NOT_ACTIVE: Employee must have at least one active contract before creating a user account.");

        var positionCode = await _eiRepository.GetPositionCodeAsync(ei.PositionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(positionCode))
            throw new DomainValidationException(
                $"POSITION_CODE_MISSING: Position {ei.PositionId} has no Code configured.");

        var employeeCode = await GenerateUniqueEmployeeCodeAsync(positionCode!, cancellationToken);
        var username = await GenerateUniqueUsernameAsync(ei.FullName, employeeCode, cancellationToken);
        var plainPassword = RandomPasswordGenerator.Generate(DefaultPasswordLength);

        var entity = new Domain.Entities.User
        {
            EmployeeCode = employeeCode,
            FullName = ei.FullName,
            RoleId = request.RoleId,
            Username = username,
            PasswordHash = _passwordHasher.Hash(plainPassword),
            Email = ei.Email!,
            Dob = ei.Dob,
            PhoneNo = ei.PhoneNo ?? string.Empty,
            Status = 1,
            PasswordChangedAt = DateTime.UtcNow,
        };

        var userId = await _repository.AddWithEmployeeInfoLinkAsync(entity, ei.Id, cancellationToken);

        await _emailSender.SendAccountCredentialsAsync(
            ei.Email!,
            ei.FullName,
            username,
            plainPassword,
            cancellationToken);

        return new UserCreateResult(userId, employeeCode, username);
    }

    private async Task<string> GenerateUniqueEmployeeCodeAsync(string positionCode, CancellationToken cancellationToken)
    {
        var yearSuffix = (DateTime.UtcNow.Year % 100).ToString("D2");
        var prefix = $"{positionCode}{yearSuffix}";

        var nextSeq = await _repository.CountByEmployeeCodePrefixAsync(prefix, cancellationToken) + 1;

        for (var attempt = 0; attempt < MaxEmployeeCodeRetries; attempt++)
        {
            var candidate = $"{prefix}{nextSeq:D4}";
            if (!await _repository.EmployeeCodeExistsAsync(candidate, null, cancellationToken))
                return candidate;
            nextSeq++;
        }

        throw new DomainValidationException("EMPLOYEE_CODE_GENERATION_FAILED: Could not generate a unique employee code.");
    }

    private async Task<string> GenerateUniqueUsernameAsync(string fullName, string employeeCode, CancellationToken cancellationToken)
    {
        var username = UsernameGenerator.Generate(fullName, employeeCode);

        // EmployeeCode is unique, so this combination should already be unique;
        // safety check in case of unusual name collisions.
        if (await _repository.UsernameExistsAsync(username, null, cancellationToken))
            throw new DomainValidationException(
                $"USERNAME_CONFLICT: Generated username '{username}' already exists.");

        return username;
    }
}
