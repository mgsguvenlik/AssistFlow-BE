using System.Security.Cryptography;
using System.Text.Json;
using Business.Interfaces;
using Business.Services.Crm.Collections.Calculation;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;
namespace Business.Services.Crm.Collections;

public sealed class CollectionContractCorrectionService(AppDataContext db) : ICollectionContractCorrectionService
{
    public async Task<ResponseModel<PagedResult<CollectionContractCorrectionItem>>> HistoryAsync(long id, int page, int size, CancellationToken ct)
    {
        if (id <= 0 || page is < 1 or > 1000000 || size is < 1 or > 100) return ResponseModel<PagedResult<CollectionContractCorrectionItem>>.Fail("Geçersiz sayfa bilgisi.");
        var q = db.Set<CollectionContractCorrection>().AsNoTracking().Where(x => x.ContractId == id);
        return ResponseModel<PagedResult<CollectionContractCorrectionItem>>.Success(new(await q.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)
            .Select(x => new CollectionContractCorrectionItem(x.Id, x.Kind, x.Reason, x.ActorUserId, x.CreatedDate, x.BeforeJson, x.AfterJson)).ToListAsync(ct), await q.CountAsync(ct), page, size));
    }
    public async Task<ResponseModel> ExecuteAsync(long id, CollectionContractCorrectionCommand c, long actor, CancellationToken ct)
    {
        if (id <= 0 || c.RequestId == Guid.Empty || !c.ConfirmImpact || c.Kind is not ("Terms" or "Rate" or "Delete" or "Stop") || string.IsNullOrWhiteSpace(c.Reason) || c.Reason.Trim().Length is < 3 or > 500)
            return ResponseModel.Fail("Geçerli düzeltme türü, işlem anahtarı, etki onayı ve 3–500 karakter gerekçe gereklidir.");
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null) return ResponseModel.Fail("Bağımsız işlem kapsamı gereklidir.", StatusCode.Conflict);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { id, Command = c with { RequestId = Guid.Empty } })));
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actor && !x.IsDeleted, ct)) return ResponseModel.Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
            var previous = await db.Set<CollectionContractCorrection>().AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == c.RequestId, ct);
            if (previous != null) return previous.ActorUserId == actor && previous.PayloadHash == hash
                ? ResponseModel.Success("Düzeltme daha önce tamamlanmış.") : ResponseModel.Fail("İşlem anahtarı farklı içerik veya kullanıcıya ait.", StatusCode.Conflict);
            if (!await CollectionCustomerScopeQuery.Contracts(db.Set<CollectionContract>(), db.Customers).AnyAsync(x => x.Id == id, ct))
                return ResponseModel.Fail(CollectionCustomerClassification.OutsideScopeMessage);
            var contract = await db.Set<CollectionContract>().SingleAsync(x => x.Id == id, ct);
            if (Convert.ToBase64String(contract.RowVersion) != c.RowVersion) return ResponseModel.Fail("Sözleşme değişmiş; detayı yenileyin.", StatusCode.Conflict);
            string before, after;
            if (c.Kind == "Stop")
            {
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Europe/Istanbul").DateTime);
                // Day-based ledger: retain all debt already due today; suspend from the next day.
                var effective = today.AddDays(1);
                var status = await db.Set<CollectionContractStatus>().AsNoTracking().SingleOrDefaultAsync(x => x.Code == "NONE" && x.IsActive, ct);
                if (status is null) return ResponseModel.Fail("YOK sözleşme durumu tanımı bulunamadı.");
                if (contract.ContractStatusId == status.Id) return ResponseModel.Fail("Sözleşme zaten YOK durumunda.", StatusCode.Conflict);
                if (contract.StartDate > today || contract.EndDate < effective) return ResponseModel.Fail("Başlamamış veya sona ermiş sözleşmede ileriye dönük durdurma yapılamaz.");
                var candidates = await db.Set<CollectionContractRatePeriod>().Where(x => x.ContractId == id && !x.IsDeleted &&
                    (x.EffectiveToExclusive == null || x.EffectiveToExclusive > today)).OrderBy(x => x.EffectiveFrom).Take(2).ToListAsync(ct);
                if (candidates.Count != 1 || candidates[0].EffectiveFrom > today || candidates[0].EffectiveToExclusive <= effective)
                    return ResponseModel.Fail("Tarife geçmişi eksik, sonlanmış veya ileri tarihli değişiklik içeriyor. Önce tarife geçmişini inceleyin.", StatusCode.Conflict);
                var current = candidates[0];
                before = JsonSerializer.Serialize(new { contract.ContractStatusId, Rate = Rate(current) });
                var next = new CollectionContractRatePeriod { ContractId = id, EffectiveFrom = effective, EffectiveToExclusive = current.EffectiveToExclusive,
                    BillingAnchor = current.BillingAnchor, OriginalAnchorDay = current.OriginalAnchorDay, PaymentFrequencyId = current.PaymentFrequencyId,
                    Amount = current.Amount, CurrencyTypeId = current.CurrencyTypeId, BillingBehavior = CollectionBillingBehavior.Suspended,
                    ChangeReason = "Sözleşme YOK: ileriye dönük durdurma", CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow };
                current.EffectiveToExclusive = effective; current.UpdatedUser = actor; current.UpdatedDate = DateTimeOffset.UtcNow;
                // Release the unique open-period index before inserting its successor, within this transaction.
                await db.SaveChangesAsync(ct);
                db.Add(next); contract.ContractStatusId = status.Id;
                await db.SaveChangesAsync(ct);
                after = JsonSerializer.Serialize(new { contract.ContractStatusId, EffectiveFrom = effective, PreviousRate = Rate(current), NextRate = Rate(next) });
            }
            else if (c.Kind == "Delete")
            {
                if (await db.Set<CollectionPayment>().AnyAsync(x => x.ContractId == id, ct) ||
                    await db.Set<CollectionContractAttachment>().AnyAsync(x => x.ContractId == id, ct) ||
                    await db.Set<CollectionContractPeriodFollowUp>().AnyAsync(x => x.ContractId == id, ct) ||
                    await db.Set<CollectionBankLoadRow>().AnyAsync(x => x.ContractId == id, ct) ||
                    await db.Set<CollectionMigrationMap>().AnyAsync(x => x.TargetContractId == id || x.TargetRatePeriod != null && x.TargetRatePeriod.ContractId == id, ct))
                    return ResponseModel.Fail("Sözleşmenin ödeme, dosya, takip, banka veya legacy aktarım bağlantısı var. Bağlı kayıtlar topluca silinmez; önce bağımlılıkları inceleyin.");
                var rates = await db.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => x.ContractId == id).Take(1001).ToListAsync(ct);
                if (rates.Count > 1000) return ResponseModel.Fail("Tarife sayısı kontrollü silme sınırını aşıyor.");
                before = JsonSerializer.Serialize(new { contract.Id, contract.CreationRequestId, contract.CustomerId, contract.ServiceTypeId,
                    contract.StartDate, contract.EndDate, contract.PaymentMethodId, contract.ContractStatusId, contract.SubscriptionStatusId,
                    contract.GtsNo, contract.IvrNo, Rates = rates.Select(Rate).ToArray() });
                after = "{}";
                await db.Set<CollectionContractRatePeriod>().Where(x => x.ContractId == id).ExecuteDeleteAsync(ct);
                db.Remove(contract);
            }
            else if (c.Kind == "Terms")
            {
                if (c.StartDate is null || c.StartDate == default(DateOnly) || c.StartDate.Value.Year == 9999 || c.EndDate < c.StartDate || c.EndDate?.Year == 9999)
                    return ResponseModel.Fail("Başlangıç/bitiş tarihleri geçersiz.");
                if (await db.Set<CollectionContractRatePeriod>().AnyAsync(x => x.ContractId == id && !x.IsDeleted && x.EffectiveFrom < c.StartDate, ct))
                    return ResponseModel.Fail("Başlangıç mevcut tarife tarihçesinden sonraya taşınamaz.");
                if (c.EndDate != contract.EndDate)
                {
                    var edge = c.EndDate is null ? contract.EndDate!.Value : contract.EndDate is null ? c.EndDate.Value
                        : c.EndDate.Value < contract.EndDate.Value ? c.EndDate.Value : contract.EndDate.Value;
                    var affectedMonth = new DateOnly(edge.Year, edge.Month, 1);
                    if (await db.Set<CollectionPayment>().AnyAsync(x => x.ContractId == id && x.Period >= affectedMonth, ct))
                        return ResponseModel.Fail("Bitiş değişikliğinin etkilediği dönemlerde ödeme var. Önce ilgili ödemeleri inceleyin.");
                }
                if (c.PaymentMethodId != contract.PaymentMethodId && c.PaymentMethodId.HasValue)
                {
                    var method = await db.Set<CollectionPaymentMethod>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == c.PaymentMethodId && x.IsActive, ct);
                    if (method == null) return ResponseModel.Fail("Ödeme yöntemi kullanıma açık değil.");
                    if (method.Code.Contains("FREE", StringComparison.OrdinalIgnoreCase) || method.Name.Contains("Ücretsiz", StringComparison.OrdinalIgnoreCase))
                        return ResponseModel.Fail("Ücretsiz hizmet için tarife değişikliği işlemini kullanın; ödeme yöntemi borç davranışını değiştirmez.");
                }
                before = Terms(contract);
                contract.StartDate = c.StartDate.Value; contract.EndDate = c.EndDate; contract.PaymentMethodId = c.PaymentMethodId;
                after = Terms(contract);
            }
            else
            {
                var rate = await db.Set<CollectionContractRatePeriod>().SingleOrDefaultAsync(x => x.Id == c.RateId && x.ContractId == id && !x.IsDeleted, ct);
                if (rate == null || Convert.ToBase64String(rate.RowVersion) != c.RateRowVersion) return ResponseModel.Fail("Tarife değişmiş veya bulunamadı; geçmişi yenileyin.", StatusCode.Conflict);
                if (c.Amount is null || c.Amount < 0 || c.Amount >= 10000000000000000m || decimal.Round(c.Amount.Value, 2) != c.Amount)
                    return ResponseModel.Fail("Geçerli, negatif olmayan ve en fazla iki ondalıklı dönem tutarı girin.");
                if (!await db.Set<Model.Concrete.CurrencyType>().AnyAsync(x => x.Id == c.CurrencyTypeId, ct)) return ResponseModel.Fail("Para birimi bulunamadı.");
                var frequency = await db.Set<CollectionPaymentFrequency>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == c.PaymentFrequencyId, ct);
                if (frequency == null || !CollectionPeriodRules.IsSupportedInterval(frequency.IntervalMonths) ||
                    frequency.Id != rate.PaymentFrequencyId && !frequency.IsActive) return ResponseModel.Fail("Ödeme dönemi geçersiz veya kullanıma kapalı.");
                var from = c.CorrectRateDates ? c.RateEffectiveFrom : rate.EffectiveFrom;
                var until = c.CorrectRateDates ? c.RateEffectiveToExclusive : rate.EffectiveToExclusive;
                if (from is null || from == default(DateOnly) || from.Value.Year == 9999 || until?.Year == 9999 || until <= from || from < rate.BillingAnchor ||
                    c.CorrectRateDates && (from < contract.StartDate || contract.EndDate.HasValue && (until is null || until.Value > contract.EndDate.Value.AddDays(1))))
                    return ResponseModel.Fail("Tarife tarihleri geçersiz. Yenileme başlangıcı ve sözleşme sınırları korunmalıdır.");
                if (await db.Set<CollectionContractRatePeriod>().AnyAsync(x => x.ContractId == id && x.Id != rate.Id && !x.IsDeleted &&
                    (until == null || x.EffectiveFrom < until) && (x.EffectiveToExclusive == null || x.EffectiveToExclusive > from), ct))
                    return ResponseModel.Fail("Tarife tarihleri başka bir tarife ile çakışıyor. Komşu dönemleri inceleyin.");
                var preceding = await db.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => x.ContractId == id && x.Id != rate.Id && !x.IsDeleted && x.EffectiveFrom < from)
                    .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
                var following = await db.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => x.ContractId == id && x.Id != rate.Id && !x.IsDeleted && x.EffectiveFrom > from)
                    .OrderBy(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
                if (preceding != null && preceding.EffectiveToExclusive != from || following != null && until != following.EffectiveFrom)
                    return ResponseModel.Fail("Tarifeler arasında boşluk bırakılamaz. Komşu dönemlerin sınırlarını koruyun.");
                var affectedFrom = from.Value < rate.EffectiveFrom ? from.Value : rate.EffectiveFrom;
                var affectedUntil = until is null || rate.EffectiveToExclusive is null ? null : until > rate.EffectiveToExclusive ? until : rate.EffectiveToExclusive;
                var firstMonth = new DateOnly(affectedFrom.Year, affectedFrom.Month, 1);
                if (await db.Set<CollectionPayment>().AnyAsync(x => x.ContractId == id && x.Period >= firstMonth &&
                    (affectedUntil == null || x.Period < affectedUntil), ct))
                    return ResponseModel.Fail("Bu tarife aralığında ödeme var. Geçmiş finansal mutabakatı korumak için önce ilgili ödemeleri inceleyin.");
                before = Rate(rate);
                rate.EffectiveFrom = from.Value; rate.EffectiveToExclusive = until;
                rate.Amount = c.Amount; rate.CurrencyTypeId = c.CurrencyTypeId; rate.PaymentFrequencyId = c.PaymentFrequencyId!.Value;
                rate.UpdatedUser = actor; rate.UpdatedDate = DateTimeOffset.UtcNow;
                after = Rate(rate);
            }
            if (c.Kind != "Delete") { contract.UpdatedUser = actor; contract.UpdatedDate = DateTimeOffset.UtcNow; }
            db.Add(new CollectionContractCorrection { ContractId = id, RequestId = c.RequestId, PayloadHash = hash, Kind = c.Kind,
                Reason = c.Reason.Trim(), BeforeJson = before, AfterJson = after, ActorUserId = actor, CreatedDate = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ResponseModel.Success("Düzeltme kaydedildi; önceki ve sonraki bilgiler işlem geçmişinde korunuyor.");
        }
        catch (DbUpdateConcurrencyException) { return ResponseModel.Fail("Kayıt eşzamanlı değişti; detayı yenileyin.", StatusCode.Conflict); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return ResponseModel.Fail("Sonuç doğrulanamadı. Aynı işlem anahtarıyla tekrar deneyin.", (StatusCode)503); }
        finally { db.ChangeTracker.Clear(); }
    }
    private static string Terms(CollectionContract x) => JsonSerializer.Serialize(new { x.StartDate, x.EndDate, x.PaymentMethodId, x.ContractStatusId });
    private static string Rate(CollectionContractRatePeriod x) => JsonSerializer.Serialize(new { x.Id, x.EffectiveFrom, x.EffectiveToExclusive, x.BillingAnchor, x.OriginalAnchorDay, x.Amount, x.CurrencyTypeId, x.PaymentFrequencyId, x.BillingBehavior });
}
