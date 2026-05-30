using MediatR;

namespace MPS.Application.Features.EmployeeInformation.Commands;

public record EmployeeInformationUpdateCommand(
    int Id,
    int UserId,
    string TaxNo,
    string SocialNo,
    string BankNumber,
    int PositionId,
    int DependentsNo,
    string CitizenId,
    string Address) : IRequest<bool>;
