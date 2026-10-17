using System.Security.Cryptography;

namespace Business.Services.Crm.Collections.Calculation;

/// <summary>Checks committed receipts only. Caller still enforces authorization and transactional uniqueness.</summary>
public static class CollectionPaymentReplayRules
{
    public static bool CanReplay(long recordedActorId, long currentActorId, byte[] recordedHash, byte[] incomingHash)
    {
        if (recordedActorId <= 0 || currentActorId <= 0)
            throw new ArgumentException("Geçerli bir kullanıcı kimliği gereklidir.");
        if (recordedHash is not { Length: 32 } || incomingHash is not { Length: 32 })
            throw new ArgumentException("İşlem içerik özeti geçersiz.");
        return recordedActorId == currentActorId && CryptographicOperations.FixedTimeEquals(recordedHash, incomingHash);
    }
}
