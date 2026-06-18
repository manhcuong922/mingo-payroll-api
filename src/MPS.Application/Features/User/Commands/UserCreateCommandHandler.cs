using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.User.Commands;

public class UserCreateCommandHandler : IRequestHandler<UserCreateCommand, UserCreateResult>
{
    private readonly IEmployeeAccountProvisioner _provisioner;
    private readonly IEmployeeInformationRepository _eiRepository;
    private readonly IEmailSender _emailSender;

    public UserCreateCommandHandler(
        IEmployeeAccountProvisioner provisioner,
        IEmployeeInformationRepository eiRepository,
        IEmailSender emailSender)
    {
        _provisioner = provisioner;
        _eiRepository = eiRepository;
        _emailSender = emailSender;
    }

    public async Task<UserCreateResult> Handle(UserCreateCommand request, CancellationToken cancellationToken)
    {
        var credentials = await _provisioner.ProvisionAsync(
            request.EmployeeInformationId, request.RoleId, cancellationToken);

        var ei = await _eiRepository.GetByIdAsync(request.EmployeeInformationId, cancellationToken);

        await _emailSender.SendAccountCredentialsAsync(
            ei!.Email!,
            ei.FullName,
            credentials.Username,
            credentials.PlainPassword,
            cancellationToken);

        return new UserCreateResult(credentials.UserId, credentials.EmployeeCode, credentials.Username);
    }
}
