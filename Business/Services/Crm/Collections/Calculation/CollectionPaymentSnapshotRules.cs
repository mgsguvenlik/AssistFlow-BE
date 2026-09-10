using System.Text.Json;
using Model.Concrete.Collections;

namespace Business.Services.Crm.Collections.Calculation;

/// <summary>Explicit audit projection: never serializes navigation graphs or arbitrary request bodies.</summary>
public static class CollectionPaymentSnapshotRules
{
    public static string Serialize(CollectionPayment payment)
    {
        if (payment is null) throw new ArgumentException("Ödeme bilgileri gereklidir.");
        return JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            payment.Id,
            payment.ContractId,
            payment.Period,
            payment.PaymentDate,
            payment.Amount,
            payment.CurrencyTypeId,
            payment.Description,
            payment.IsFree,
            payment.CreatedDate,
            payment.UpdatedDate,
            payment.CreatedUser,
            payment.UpdatedUser,
            RowVersion = Convert.ToBase64String(payment.RowVersion)
        });
    }
}
