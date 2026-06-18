using System.Security.Cryptography;
using System.Text;

namespace MPS.Infrastructure.ComplianceAutoUpdate.Common;

public static class Hashing
{
    /// <summary>SHA256 hex (64 ký tự, lowercase).</summary>
    public static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
