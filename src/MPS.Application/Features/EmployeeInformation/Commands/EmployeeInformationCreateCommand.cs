using MediatR;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public record EmployeeInformationCreateCommand(
    int UserId,
    string TaxNo,
    string SocialNo,
    string BankNumber,
    int PositionId,
    int DependentsNo,
    string CitizenId,
    string Address) : IRequest<int>;
