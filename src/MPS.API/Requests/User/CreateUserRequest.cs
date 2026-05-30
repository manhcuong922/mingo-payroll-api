namespace MPS.API.Requests.User;
public sealed record CreateUserRequest(
    string EmployeeCode,
    string FullName,
    int RoleId,
    string Username,
    string Password,
    string Email,
    DateTime Dob,
    string PhoneNo,
    int Status);
