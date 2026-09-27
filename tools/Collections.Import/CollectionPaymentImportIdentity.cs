using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>Batch'ten bağımsız, PaymentOperation içinde silmeden sonra da korunan kaynak kimliği.</summary>
internal static class CollectionPaymentImportIdentity
{
    public static Guid For(long sourcePaymentId)
    {
        if (sourcePaymentId <= 0) throw new ArgumentOutOfRangeException(nameof(sourcePaymentId));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("AssistFlow:collection:legacy-payment:v1:MGS:" + sourcePaymentId.ToString(CultureInfo.InvariantCulture)));
        return new Guid(hash.AsSpan(0, 16));
    }
}
