using Business.Interfaces.Helpdesk;
using Microsoft.AspNetCore.DataProtection;

namespace WebAPI;

public sealed class HelpdeskSecretProtector : IHelpdeskSecretProtector
{
    private readonly IDataProtector _protector;

    public HelpdeskSecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("AssistFlow.Helpdesk.MailboxPassword.v1");

    public string Protect(string plaintext)
        => string.IsNullOrWhiteSpace(plaintext)
            ? throw new ArgumentException("Parola boş olamaz.", nameof(plaintext))
            : _protector.Protect(plaintext);

    public string Unprotect(string protectedValue)
        => _protector.Unprotect(protectedValue);
}
