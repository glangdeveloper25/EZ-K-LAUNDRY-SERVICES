using System.ComponentModel.DataAnnotations;
using EZKLaundrySystem.Data;

namespace EZKLaundrySystem.Models;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class StrongPasswordAttribute : ValidationAttribute
{
    public StrongPasswordAttribute() : base(PasswordSecurity.StrengthError)
    {
    }

    public override bool IsValid(object? value) =>
        value is string password && PasswordSecurity.IsStrongPassword(password);
}
