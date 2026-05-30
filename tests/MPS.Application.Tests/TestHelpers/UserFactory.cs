using MPS.Domain.Entities;

namespace MPS.Application.Tests.TestHelpers;

internal static class UserFactory
{
    public static User CreateActive(
        int id = 1,
        string username = "alice",
        string passwordHash = "hash",
        int roleId = 7,
        string roleName = "Employee",
        int status = 1,
        string email = "alice@mingo.local")
    {
        return new User
        {
            Id = id,
            EmployeeCode = "EC" + id.ToString("D3"),
            FullName = username + " full",
            Username = username,
            PasswordHash = passwordHash,
            Email = email,
            RoleId = roleId,
            Role = new Role
            {
                Id = roleId,
                Name = roleName,
                IsSystem = true,
                CreatedAt = DateTime.UtcNow,
            },
            Status = status,
            Dob = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PhoneNo = "0000000000",
        };
    }
}
