using MPS.Application.Common.Interfaces;

namespace MPS.Infrastructure.Security;

public sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string plainPassword)
    {
        if (string.IsNullOrEmpty(plainPassword))
            throw new ArgumentException("Password must not be empty.", nameof(plainPassword));

        return BCrypt.Net.BCrypt.HashPassword(plainPassword, WorkFactor);
    }

    public bool Verify(string plainPassword, string passwordHash)
    {
        if (string.IsNullOrEmpty(plainPassword) || string.IsNullOrEmpty(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, passwordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
