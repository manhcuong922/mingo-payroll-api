namespace MPS.Application.Common.Interfaces;

public interface IEmployeeAccountProvisioner
{
    Task<EmployeeAccountCredentials> ProvisionAsync(
        int employeeInformationId,
        int roleId,
        CancellationToken cancellationToken = default);
}

public sealed record EmployeeAccountCredentials(
    int UserId,
    string EmployeeCode,
    string Username,
    string PlainPassword);
