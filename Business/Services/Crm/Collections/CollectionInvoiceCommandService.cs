using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Business.Interfaces;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionInvoiceCommandService(AppDataContext db) : ICollectionInvoiceCommandService
{
    public async Task<ResponseModel<CollectionInvoiceCommit>> ExecuteAsync(long invoiceId, long? paymentId, string kind,
        CollectionInvoiceCommand command, long actor, CancellationToken ct)
    {
        if (invoiceId <= 0 || actor <= 0 || command.RequestId == Guid.Empty || command.InvoiceRowVersion?.Length != 8 ||
            kind is not ("PaymentCreate" or "PaymentUpdate" or "PaymentDelete" or "CommentUpdate" or "InvoiceDelete"))
            return Fail("İşlem bilgileri veya fatura sürümü geçersiz.");
        if (kind is "PaymentUpdate" or "PaymentDelete" && (paymentId is null or <= 0 || command.PaymentRowVersion?.Length != 8))
            return Fail("Ödeme kaydı ve güncel kayıt sürümü gereklidir.");
        if (kind is "PaymentCreate" or "PaymentUpdate" &&
            (command.Date is null || command.Date == default(DateOnly) || command.Amount is null ||
             Math.Abs(command.Amount.Value) >= 10000000000000000m || decimal.Round(command.Amount.Value, 2) != command.Amount.Value))
            return Fail("Geçerli tarih ve en fazla iki ondalıklı tutar girin.");
        if (command.Description?.Length > (kind == "CommentUpdate" ? 10000 : 1000)) return Fail("Açıklama izin verilen uzunluğu aşıyor.");
        if (kind == "PaymentCreate" && command.Amount <= 0) return Fail("Yeni ödeme tutarı sıfırdan büyük olmalıdır.");
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries().Any()) return Fail("İşlem için temiz bir çalışma kapsamı gereklidir.", StatusCode.Conflict);
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { invoiceId, paymentId, kind, command }));
        try
        {
            if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actor && !x.IsDeleted, ct)) return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                try
                {
                    var previous = await db.Set<CollectionInvoiceOperation>().AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == command.RequestId, ct);
                    if (previous is not null)
                        return previous.ActorUserId == actor && previous.PayloadHash.SequenceEqual(hash)
                            ? ResponseModel<CollectionInvoiceCommit>.Success(new(previous.InvoiceId, previous.PaymentId, true), "İşlem daha önce tamamlanmış.")
                            : Fail("İşlem anahtarı farklı içerik veya kullanıcıyla kullanılmış.", StatusCode.Conflict);
                    var customers = CollectionCustomerScopeQuery.Customers(db.Customers).Select(x => x.Id);
                    var invoice = await db.Set<CollectionInvoice>().SingleOrDefaultAsync(x => x.Id == invoiceId && customers.Contains(x.CustomerId), ct);
                    if (invoice is null) return Fail("Tahsilat kapsamında fatura bulunamadı.", StatusCode.NotFound);
                    if (!invoice.RowVersion.SequenceEqual(command.InvoiceRowVersion)) return Fail("Fatura değişmiş. Güncel bilgileri yükleyip tekrar deneyin.", StatusCode.Conflict);
                    db.Entry(invoice).Property(x => x.RowVersion).OriginalValue = command.InvoiceRowVersion;
                    CollectionInvoicePayment? payment = null;
                    if (kind is "PaymentUpdate" or "PaymentDelete")
                    {
                        payment = await db.Set<CollectionInvoicePayment>().SingleOrDefaultAsync(x => x.Id == paymentId && x.InvoiceId == invoiceId, ct);
                        if (payment is null) return Fail("Faturaya ait ödeme bulunamadı.", StatusCode.NotFound);
                        if (!payment.RowVersion.SequenceEqual(command.PaymentRowVersion!)) return Fail("Ödeme değişmiş. Listeyi yenileyin.", StatusCode.Conflict);
                        db.Entry(payment).Property(x => x.RowVersion).OriginalValue = command.PaymentRowVersion!;
                        if (kind == "PaymentUpdate" && command.Amount <= 0 && command.Amount != payment.Amount)
                            return Fail("Yeni ödeme tutarı sıfırdan büyük olmalıdır; tarihsel sıfır/negatif tutar ancak değiştirilmeden korunabilir.");
                    }
                    var before = kind == "PaymentCreate" ? null : kind.StartsWith("Payment") ? Snapshot(payment!) : Snapshot(invoice);
                    if (kind == "InvoiceDelete")
                    {
                        if (await db.Set<CollectionInvoicePayment>().AnyAsync(x => x.InvoiceId == invoiceId, ct))
                            return Fail("Ödemesi bulunan fatura silinemez. Önce bağlı ödemeleri kontrol edin.", StatusCode.Conflict);
                        db.Remove(invoice);
                    }
                    else if (kind == "CommentUpdate") invoice.Comment = command.Description;
                    else
                    {
                        // Refresh invoice rowversion as a balance concurrency token, including payment-only mutations.
                        db.Entry(invoice).Property(x => x.Comment).IsModified = true;
                        if (kind == "PaymentDelete") db.Remove(payment!);
                        else
                        {
                            if (kind == "PaymentCreate")
                            {
                                payment = new CollectionInvoicePayment { InvoiceId = invoiceId, CreatedDate = DateTimeOffset.UtcNow, CreatedUser = actor };
                                db.Add(payment);
                            }
                            payment!.Date = command.Date!.Value;
                            payment.Amount = command.Amount!.Value;
                            payment.Description = command.Description;
                        }
                    }
                    await db.SaveChangesAsync(ct);
                    db.Add(new CollectionInvoiceOperation
                    {
                        RequestId = command.RequestId, ActorUserId = actor, Kind = kind, InvoiceId = invoiceId,
                        PaymentId = payment?.Id, LegacyInvoiceId = invoice.LegacyInvoiceFollowId,
                        LegacyPaymentId = payment?.LegacyInvoiceFollowPaymentId, PayloadHash = hash, BeforeJson = before,
                        AfterJson = kind is "InvoiceDelete" or "PaymentDelete" ? null : kind.StartsWith("Payment") ? Snapshot(payment!) : Snapshot(invoice),
                        CompletedDate = DateTimeOffset.UtcNow
                    });
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return ResponseModel<CollectionInvoiceCommit>.Success(new(invoiceId, payment?.Id, false), "Fatura işlemi tamamlandı.");
                }
                finally { db.ChangeTracker.Clear(); }
            });
        }
        catch (DbUpdateConcurrencyException) { return Fail("Kayıt başka bir işlem tarafından değiştirildi. Güncel bilgileri yükleyin.", StatusCode.Conflict); }
        catch (Exception ex) when (ex is DbUpdateException or SqlException or TimeoutException || ex.GetBaseException() is SqlException)
        { return Fail("İşlem sonucu doğrulanamadı. Aynı işlem anahtarıyla tekrar deneyin; çift kayıt oluşturulmaz.", (StatusCode)503); }
    }
    private static string Snapshot(CollectionInvoicePayment x) => JsonSerializer.Serialize(new { x.Id, x.InvoiceId, x.Date, x.Amount, x.Description, x.RowVersion, x.LegacyInvoiceFollowPaymentId });
    private static string Snapshot(CollectionInvoice x) => JsonSerializer.Serialize(new { x.Id, x.CustomerId, x.Type, x.Number, x.Date, x.Amount, x.CurrencyTypeId, x.ProjectCode, x.Comment, x.RowVersion, x.LegacyInvoiceFollowId });
    private static ResponseModel<CollectionInvoiceCommit> Fail(string message, StatusCode code = StatusCode.BadRequest) => ResponseModel<CollectionInvoiceCommit>.Fail(message, code);
}
