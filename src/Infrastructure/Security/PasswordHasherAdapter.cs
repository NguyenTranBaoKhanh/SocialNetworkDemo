using Application.Common;
using Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Security;

/// <summary>
/// Bọc PasswordHasher của ASP.NET Core Identity (PBKDF2) sau interface của Application.
/// </summary>
public class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public bool Verify(string hash, string password)
    {
        var result = _inner.VerifyHashedPassword(null!, hash, password);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
