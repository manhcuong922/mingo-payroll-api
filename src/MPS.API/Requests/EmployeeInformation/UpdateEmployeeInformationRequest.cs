namespace MPS.API.Requests.EmployeeInformation;
public sealed record UpdateEmployeeInformationRequest(
    string EmployeeCode,
    string TaxNo,
    string SocialNo,
    string BankNumber,
    int PositionId,
    int DependentsNo,
    string CitizenId,
    string Address);