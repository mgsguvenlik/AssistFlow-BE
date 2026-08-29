namespace Business.Interfaces.Helpdesk;

public interface IHelpdeskSecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}
