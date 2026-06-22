using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Utilities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Common.Services;

public sealed class EmployeeAccountProvisioner : IEmployeeAccountProvisioner
{
    private const int DefaultPasswordLength = 8;
    private const int MaxEmployeeCodeRetries = 10;

    private readonly IUserRepository _userRepository;
    private readonly IEmployeeInformationRepository _eiRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IPasswordHasher _passwordHasher;

    public EmployeeAccountProvisioner(
        IUserRepository userRepository,
        IEmployeeInformationRepository eiRepository,
        IContractRepository contractRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _eiRepository = eiRepository;
        _contractRepository = contractRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<EmployeeAccountCredentials> ProvisionAsync(
        int employeeInformationId,
        int roleId,
        CancellationToken cancellationToken = default)
    {
        var ei = await _eiRepository.GetByIdAsync(employeeInformationId, cancellationToken)
            ?? throw new DomainValidationException(
                $"EMPLOYEE_INFO_NOT_FOUND: EmployeeInformation {employeeInformationId} does not exist.");

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
            RoleId = roleId,
            Username = username,
            PasswordHash = _passwordHasher.Hash(plainPassword),
            Email = ei.Email!,
            Dob = ei.Dob,
            PhoneNo = ei.PhoneNo ?? string.Empty,
            Status = 1,
            PasswordChangedAt = DateTime.UtcNow,
        };

        var userId = await _userRepository.AddWithEmployeeInfoLinkAsync(entity, ei.Id, cancellationToken);

        return new EmployeeAccountCredentials(userId, employeeCode, username, plainPassword);
    }

    private async Task<string> GenerateUniqueEmployeeCodeAsync(string positionCode, CancellationToken cancellationToken)
    {
        var yearSuffix = (DateTime.UtcNow.Year % 100).ToString("D2");
        var prefix = $"{positionCode}{yearSuffix}";

        var nextSeq = await _userRepository.CountByEmployeeCodePrefixAsync(prefix, cancellationToken) + 1;

        for (var attempt = 0; attempt < MaxEmployeeCodeRetries; attempt++)
        {
            var candidate = $"{prefix}{nextSeq:D4}";
            if (!await _userRepository.EmployeeCodeExistsAsync(candidate, null, cancellationToken))
                return candidate;
            nextSeq++;
        }

        throw new DomainValidationException("EMPLOYEE_CODE_GENERATION_FAILED: Could not generate a unique employee code.");
    }

    private async Task<string> GenerateUniqueUsernameAsync(string fullName, string employeeCode, CancellationToken cancellationToken)
    {
        var username = UsernameGenerator.Generate(fullName, employeeCode);

        if (await _userRepository.UsernameExistsAsync(username, null, cancellationToken))
            throw new DomainValidationException(
                $"USERNAME_CONFLICT: Generated username '{username}' already exists.");

        return username;
    }
}
