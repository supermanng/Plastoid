using Microsoft.AspNetCore.Identity;

namespace Plastoids.Infrastructure.Security;

/// <summary>PBKDF2 (ASP.NET Core Identity v3 format) password hashing. Rehash-on-login when the format is outdated.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerification Verify(string hash, string password);
}

public enum PasswordVerification
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2,
}

internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<object> Inner = new();
    private static readonly object Dummy = new();

    public string Hash(string password) => Inner.HashPassword(Dummy, password);

    public PasswordVerification Verify(string hash, string password) => Inner.VerifyHashedPassword(Dummy, hash, password) switch
    {
        PasswordVerificationResult.Success => PasswordVerification.Success,
        PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
        _ => PasswordVerification.Failed,
    };
}
