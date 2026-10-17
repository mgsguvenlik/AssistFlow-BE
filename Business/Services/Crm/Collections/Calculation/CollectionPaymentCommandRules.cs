using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections.Calculation;

/// <summary>Versioned canonical content hash. Does not validate authorization, existence or financial eligibility.</summary>
public static class CollectionPaymentCommandRules
{
    public static byte[] ComputeHash(CollectionPaymentCommand command)
    {
        if (command is null) throw new ValidationException("Ödeme işlem bilgileri gereklidir.");
        if (!Enum.IsDefined(command.Kind)) throw new ValidationException("Ödeme işlem türü geçersiz.");
        byte[]? rowVersion = null;
        if (command.Kind == CollectionPaymentOperationKind.Create)
        {
            if (command.PaymentId is not null || command.ExpectedRowVersion is not null)
                throw new ValidationException("Yeni ödemede mevcut kayıt kimliği veya sürüm gönderilemez.");
        }
        else
        {
            if (command.PaymentId is null or <= 0) throw new ValidationException("Geçerli bir ödeme kimliği gereklidir.");
            try { rowVersion = Convert.FromBase64String(command.ExpectedRowVersion ?? string.Empty); }
            catch (FormatException) { throw new ValidationException("Ödeme kayıt sürümü geçersiz."); }
            if (rowVersion.Length != 8) throw new ValidationException("Ödeme kayıt sürümü geçersiz.");
        }
        if (command.Kind == CollectionPaymentOperationKind.Delete)
        {
            if (command.ContractId is not null || command.Period is not null || command.PaymentDate is not null
                || command.Amount is not null || command.CurrencyTypeId is not null || command.Description is not null || command.IsFree is not null)
                throw new ValidationException("Silme işleminde değiştirilecek ödeme alanları gönderilemez.");
        }
        else
        {
            if (command.ContractId is null or <= 0 || command.CurrencyTypeId is null or <= 0)
                throw new ValidationException("Geçerli sözleşme ve para birimi gereklidir.");
            if (command.Period is null || command.Period.Value.Day != 1 || command.PaymentDate is null)
                throw new ValidationException("Ödeme tarihi ve ayın ilk günüyle belirtilen muhasebe dönemi gereklidir.");
            if (command.Amount is null or < -9999999999999999.99m or > 9999999999999999.99m
                || decimal.Round(command.Amount.Value, 2) != command.Amount.Value)
                throw new ValidationException("Tutar en fazla iki ondalık haneli ve desteklenen aralıkta olmalıdır.");
            if (command.Description?.Length > 1000) throw new ValidationException("Ödeme açıklaması en fazla 1000 karakter olabilir.");
            if (command.IsFree is null) throw new ValidationException("Ücretsiz ödeme bilgisi gereklidir.");
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteNumber("kind", (byte)command.Kind);
            if (command.PaymentId.HasValue) writer.WriteNumber("paymentId", command.PaymentId.Value);
            if (rowVersion is not null) writer.WriteBase64String("expectedRowVersion", rowVersion);
            if (command.Kind != CollectionPaymentOperationKind.Delete)
            {
                writer.WriteNumber("contractId", command.ContractId!.Value);
                writer.WriteString("period", command.Period!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.WriteString("paymentDate", command.PaymentDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.WriteString("amount", command.Amount!.Value.ToString("G29", CultureInfo.InvariantCulture));
                writer.WriteNumber("currencyTypeId", command.CurrencyTypeId!.Value);
                writer.WriteString("description", command.Description);
                writer.WriteBoolean("isFree", command.IsFree!.Value);
            }
            writer.WriteEndObject();
        }
        return SHA256.HashData(buffer.ToArray());
    }
}
