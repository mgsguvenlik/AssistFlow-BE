namespace Business.Interfaces;

public enum SmsSendStatus { Simulation, Accepted, Rejected, Unknown }

// Accepted means the provider accepted the package, not recipient delivery.
public sealed record SmsSendResult(SmsSendStatus Status, string Message, string? PackageId = null,
    string? ErrorCode = null);

public interface ISmsSender
{
    Task<SmsSendResult> SendAsync(string phone, string message, string correlationId,
        CancellationToken cancellationToken = default);
}
