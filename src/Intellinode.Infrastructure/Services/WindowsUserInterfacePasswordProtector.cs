using Intellinode.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsUserInterfacePasswordProtector : IWindowsUserInterfacePasswordProtector
{
    private const string Purpose = "Intellinode.WindowsUserInterface.AutologonPassword.v1";
    private readonly IDataProtector _protector;

    public WindowsUserInterfacePasswordProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);
        return _protector.Protect(plaintext);
    }

    public string Unprotect(string cipher)
    {
        ArgumentException.ThrowIfNullOrEmpty(cipher);
        return _protector.Unprotect(cipher);
    }

    public bool TryUnprotect(string? cipher, out string plaintext)
    {
        plaintext = string.Empty;
        if (string.IsNullOrWhiteSpace(cipher))
        {
            return false;
        }

        try
        {
            plaintext = _protector.Unprotect(cipher);
            return true;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException)
        {
            return false;
        }
    }
}
