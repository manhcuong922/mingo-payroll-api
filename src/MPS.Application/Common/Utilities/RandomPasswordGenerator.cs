using System.Security.Cryptography;
using System.Text;

namespace MPS.Application.Common.Utilities;

/// Generates cryptographically random passwords guaranteed to contain
/// at least one uppercase letter, one lowercase letter, and one digit.
public static class RandomPasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string All = Upper + Lower + Digits;

    public static string Generate(int length = 8)
    {
        if (length < 4)
            throw new ArgumentOutOfRangeException(nameof(length), "Password length must be at least 4.");

        var chars = new char[length];
        chars[0] = PickFrom(Upper);
        chars[1] = PickFrom(Lower);
        chars[2] = PickFrom(Digits);
        for (var i = 3; i < length; i++)
            chars[i] = PickFrom(All);

        Shuffle(chars);
        return new string(chars);
    }

    private static char PickFrom(string pool)
    {
        var index = RandomNumberGenerator.GetInt32(pool.Length);
        return pool[index];
    }

    private static void Shuffle(char[] buffer)
    {
        for (var i = buffer.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
        }
    }
}
