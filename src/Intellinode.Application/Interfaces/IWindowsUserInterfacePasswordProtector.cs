namespace Intellinode.Application.Interfaces;

/// <summary>Encrypts/decrypts autologon passwords at rest.</summary>
public interface IWindowsUserInterfacePasswordProtector
{
    string Protect(string plaintext);
    string Unprotect(string cipher);
    bool TryUnprotect(string? cipher, out string plaintext);
}
