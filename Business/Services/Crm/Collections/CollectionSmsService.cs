using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Business.Services.Sms;
using Core.Common;
using Core.Enums;
using Core.Settings.Concrete;
using Core.Utilities.Constants;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionSmsService(AppDataContext db, IOptions<SmsServiceOptions> options, IHostEnvironment environment)
{
    public bool Enabled => options.Value.CollectionSmsEnabled;
    private bool Simulation => !environment.IsProduction() || options.Value.SmsSimulationEnabled;
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // The caller owns the rate-change transaction. No external calls happen here.
    public async Task<string?> QueueRateAsync(CollectionContractRatePeriod previous, CollectionContractRatePeriod next,
        long actorId, CancellationToken ct)
    {
        if (!Enabled || next.BillingBehavior != CollectionBillingBehavior.Billable || previous.Amount is null
            || next.Amount is null || next.Amount <= previous.Amount) return null;
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("SMS kuyruğu zam transactionı içinde yazılmalıdır.");
        var prepared = await PrepareAsync(next.ContractId, next.Id, ct);
        var notification = CreateNotification(next.ContractId, next.Id, previous.Amount.Value, next.Amount.Value,
            prepared?.Preview.IncreasePercent, next.EffectiveFrom);
        AddAttempt(notification, prepared, actorId, true, next.ChangeReason ?? "Zam bildirimi");
        db.Add(notification);
        await db.SaveChangesAsync(ct);
        return notification.Status == CollectionSmsStatus.Failed
            ? "SMS hazırlanamadı; nedeni sözleşmenin SMS geçmişine kaydedildi."
            : "Zam SMS bildirimi kuyruğa alındı; sonucu SMS geçmişinden takip edilebilir.";
    }

    public async Task<ResponseModel<CollectionSmsPreview>> PreviewAsync(long contractId, CancellationToken ct)
    {
        if (!Enabled) return ResponseModel<CollectionSmsPreview>.Fail("SMS bildirimi henüz kullanıma açılmadı.", (StatusCode)503);
        var prepared = await PrepareAsync(contractId, null, ct);
        if (prepared is null) return ResponseModel<CollectionSmsPreview>.Fail("Bu sözleşmede bildirilebilecek güncel bir fiyat artışı bulunamadı.");
        return ResponseModel<CollectionSmsPreview>.Success(prepared.Preview, "Zam SMS önizlemesi hazırlandı.");
    }

    public async Task<ResponseModel> SendAsync(long contractId, CollectionSmsSendCommand command, long actorId, CancellationToken ct)
    {
        if (!Enabled) return ResponseModel.Fail("SMS bildirimi henüz kullanıma açılmadı.", (StatusCode)503);
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(command, new ValidationContext(command), errors, true)
            || string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length < 3)
            return ResponseModel.Fail("Geçerli mesaj önizlemesi ve 3–500 karakterlik gönderim nedeni gereklidir.");
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null)
            return ResponseModel.Fail("İşlem şu anda tamamlanamıyor. Tekrar deneyin.", StatusCode.Conflict);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                if (actorId <= 0 || !await db.Users.AnyAsync(x => x.Id == actorId && !x.IsDeleted, ct))
                    return ResponseModel.Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
                var prepared = await PrepareAsync(contractId, command.RatePeriodId, ct);
                if (prepared is null) return ResponseModel.Fail("Bildirilecek zam kaydı bulunamadı.");
                if (prepared.Preview.PreviewHash != command.PreviewHash)
                    return ResponseModel.Fail("Mesaj, telefon veya zam bilgileri değişmiş. Önizlemeyi yenileyin.", StatusCode.Conflict);
                if (!prepared.Preview.CanSend)
                    return ResponseModel.Fail(prepared.Preview.BlockReason ?? "Bu zam için yeniden SMS gönderilemez.", StatusCode.Conflict);
                var notification = await db.Set<CollectionSmsNotification>().SingleOrDefaultAsync(x => x.RatePeriodId == command.RatePeriodId, ct)
                    ?? CreateNotification(contractId, command.RatePeriodId, prepared.Preview.OldAmount,
                        prepared.Preview.NewAmount, prepared.Preview.IncreasePercent, prepared.Preview.EffectiveDate);
                if (notification.Id == 0) db.Add(notification);
                AddAttempt(notification, prepared, actorId, false, command.Reason.Trim());
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return ResponseModel.Success("Zam SMS bildirimi kuyruğa alındı. Sonucu gönderim geçmişinden takip edebilirsiniz.");
            }
            catch (DbUpdateException)
            {
                return ResponseModel.Fail("SMS kaydı eşzamanlı değişti. Önizleme ve geçmişi yenileyin.", StatusCode.Conflict);
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                return ResponseModel.Fail("SMS kaydı tamamlanamadı. Geçmişi kontrol edip önizlemeyi yenileyin.", StatusCode.Conflict);
            }
            finally
            {
                foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is CollectionSmsNotification or CollectionSmsAttempt).ToArray())
                    entry.State = EntityState.Detached;
            }
        });
    }

    public async Task<ResponseModel<CollectionSmsHistory>> HistoryAsync(long contractId, int page, int pageSize,
        byte? status, CancellationToken ct)
    {
        if (!Enabled) return ResponseModel<CollectionSmsHistory>.Fail("SMS bildirimi henüz kullanıma açılmadı.", (StatusCode)503);
        if (page < 1 || page > 100000 || pageSize is not (25 or 50 or 100) || status > 5)
            return ResponseModel<CollectionSmsHistory>.Fail("SMS geçmişi sayfa veya durum filtresi geçersiz.");
        if (!await db.Set<CollectionContract>().AnyAsync(x => x.Id == contractId && !x.IsDeleted, ct))
            return ResponseModel<CollectionSmsHistory>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
        var query = db.Set<CollectionSmsAttempt>().AsNoTracking().Where(x => x.Notification.ContractId == contractId);
        if (status.HasValue) query = query.Where(x => (byte)x.Status == status.Value);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new CollectionSmsHistoryItem(x.Id, x.NotificationId, x.Notification.RatePeriodId, x.Sequence,
                x.CreatedDate, x.CompletedDate, x.CreatedUser, x.Phone, x.Message, x.Reason, x.IsAutomatic,
                x.IsSimulation, (byte)x.Status, x.ResultMessage, x.ErrorCode, x.PackageId,
                x.Notification.EffectiveDate, x.Notification.OldAmount, x.Notification.NewAmount)).ToListAsync(ct);
        return ResponseModel<CollectionSmsHistory>.Success(new(items, count), "SMS gönderim geçmişi getirildi.");
    }

    private sealed record Prepared(CollectionSmsPreview Preview, long? RecipientId, string Template, string? ErrorCode);

    public async Task<ResponseModel<CollectionSmsTracking>> TrackingAsync(CollectionSmsTrackingQuery request, CancellationToken ct)
    {
        if (!Enabled) return ResponseModel<CollectionSmsTracking>.Fail("SMS bildirimi henüz kullanıma açılmadı.", (StatusCode)503);
        if (request.StartDate == default || request.EndDate < request.StartDate || request.EndDate.Year >= 9999
            || request.EndDate.DayNumber - request.StartDate.DayNumber > 366 || request.Page is < 1 or > 100000
            || request.PageSize is not (25 or 50 or 100) || request.Status > 5 || request.Search?.Length > 200)
            return ResponseModel<CollectionSmsTracking>.Fail("Geçerli tarih aralığı (en fazla 367 gün), durum ve sayfa bilgisi girin.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        DateTimeOffset Utc(DateOnly date) => new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), zone));
        var start = Utc(request.StartDate); var end = Utc(request.EndDate.AddDays(1));
        var query = from a in db.Set<CollectionSmsAttempt>().AsNoTracking()
                    join c in db.Set<CollectionContract>().AsNoTracking() on a.Notification.ContractId equals c.Id into contracts
                    from c in contracts.DefaultIfEmpty()
                    join customer in db.Customers.AsNoTracking() on c.CustomerId equals customer.Id into customers
                    from customer in customers.DefaultIfEmpty()
                    join service in db.ServiceTypes.AsNoTracking() on c.ServiceTypeId equals service.Id into services
                    from service in services.DefaultIfEmpty()
                    where a.CreatedDate >= start && a.CreatedDate < end
                        && (!request.LatestOnly || a.Sequence == a.Notification.AttemptCount)
                    select new { a, c, customer, service };
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.customer.SubscriberCode != null && x.customer.SubscriberCode.Contains(search)
                || x.customer.SubscriberCompany != null && x.customer.SubscriberCompany.Contains(search)
                || x.a.Phone.Contains(search) || x.a.ErrorCode != null && x.a.ErrorCode.Contains(search));
        }
        // Counts describe the date/search/latest filters, before selecting an individual status.
        var counts = await query.GroupBy(x => x.a.Status).Select(x => new CollectionSmsStatusCount((byte)x.Key, x.Count())).ToListAsync(ct);
        if (request.Status.HasValue) query = query.Where(x => (byte)x.a.Status == request.Status.Value);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.a.CreatedDate).ThenByDescending(x => x.a.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new CollectionSmsTrackingItem(x.a.Notification.ContractId, x.customer.SubscriberCode,
                x.customer.SubscriberCompany, x.service.Name, x.c != null && !x.c.IsDeleted,
                new CollectionSmsHistoryItem(x.a.Id, x.a.NotificationId, x.a.Notification.RatePeriodId, x.a.Sequence,
                    x.a.CreatedDate, x.a.CompletedDate, x.a.CreatedUser, x.a.Phone, x.a.Message, x.a.Reason,
                    x.a.IsAutomatic, x.a.IsSimulation, (byte)x.a.Status, x.a.ResultMessage, x.a.ErrorCode,
                    x.a.PackageId, x.a.Notification.EffectiveDate, x.a.Notification.OldAmount, x.a.Notification.NewAmount))).ToListAsync(ct);
        return ResponseModel<CollectionSmsTracking>.Success(new(items, count, counts), "SMS takip listesi getirildi.");
    }

    private async Task<Prepared?> PrepareAsync(long contractId, long? rateId, CancellationToken ct)
    {
        var contract = await CollectionCustomerScopeQuery.Contracts(db.Set<CollectionContract>().AsNoTracking(), db.Customers)
            .Where(x => x.Id == contractId)
            .Select(x => new { x.CustomerId, x.Customer.SubscriberCode, x.Customer.CustomerGroupId,
                GroupCode = x.Customer.CustomerGroup != null ? x.Customer.CustomerGroup.Code : null,
                Service = x.ServiceType.Name }).SingleOrDefaultAsync(ct);
        if (contract is null) return null;
        // Only the latest rate is eligible; obsolete/removed future increases cannot be resent.
        var rate = await db.Set<CollectionContractRatePeriod>().AsNoTracking()
            .Where(x => x.ContractId == contractId && !x.IsDeleted)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (rate is null || rateId.HasValue && rate.Id != rateId || rate.BillingBehavior != CollectionBillingBehavior.Billable || rate.Amount is null) return null;
        var previous = await db.Set<CollectionContractRatePeriod>().AsNoTracking()
            .Where(x => x.ContractId == contractId && !x.IsDeleted && x.EffectiveToExclusive == rate.EffectiveFrom)
            .OrderByDescending(x => x.EffectiveFrom).Take(2).ToListAsync(ct);
        if (previous.Count != 1 || previous[0].Amount is null || rate.Amount <= previous[0].Amount) return null;
        var old = previous[0].Amount!.Value;
        decimal? percent = old > 0 ? decimal.Round((rate.Amount.Value - old) / old * 100, 8) : null;
        var recipientId = (long?)contract.CustomerId;
        string? error = null;
        string? errorCode = null;
        if (CollectionCustomerClassification.Classify(contract.GroupCode) == CollectionCustomerClass.Group)
        {
            recipientId = await (from p in db.Set<CollectionGroupParent>().AsNoTracking()
                                 join c in db.Customers.AsNoTracking() on p.CustomerId equals c.Id
                                 where p.CustomerGroupId == contract.CustomerGroupId && c.CustomerGroupId == p.CustomerGroupId
                                     && !c.IsDeleted && c.CustomerType != null && c.CustomerType.Code == "G"
                                 select (long?)c.Id).SingleOrDefaultAsync(ct);
            if (recipientId is null) { error = "Grup sorumlusunun üst müşteri kartı eşleşmesi bulunamadı; üyeye gönderilmez."; errorCode = "GROUP_RECIPIENT_MISSING"; }
        }
        var recipient = recipientId.HasValue
            ? await db.Customers.AsNoTracking().Where(x => x.Id == recipientId && !x.IsDeleted)
                .Select(x => new { x.SubscriberCompany, x.Phone1, x.Phone2, x.ContactName1, x.ContactName2 }).SingleOrDefaultAsync(ct) : null;
        var phone = SmsPhone.Select(recipient?.Phone1, recipient?.Phone2);
        var name = (SmsPhone.Normalize(recipient?.Phone1) is not null ? recipient?.ContactName1 : recipient?.ContactName2);
        if (string.IsNullOrWhiteSpace(name)) name = recipient?.SubscriberCompany;
        if (phone is null && error is null) { error = "Alıcı müşteri kartında geçerli Phone1 veya Phone2 cep telefonu bulunamadı."; errorCode = "INVALID_PHONE"; }
        var templates = await db.Configurations.AsNoTracking().Where(x => x.Name == CollectionSmsConstants.RateChangeTemplate)
            .Select(x => x.Value).Take(2).ToListAsync(ct);
        var template = templates.Count == 1 ? templates[0] : "";
        var frequency = await db.Set<CollectionPaymentFrequency>().Where(x => x.Id == rate.PaymentFrequencyId).Select(x => x.Name).SingleOrDefaultAsync(ct);
        var currency = await db.CurrencyTypes.Where(x => x.Id == rate.CurrencyTypeId).Select(x => x.Code).SingleOrDefaultAsync(ct);
        string message = "";
        if (error is null && (percent is null || previous[0].CurrencyTypeId != rate.CurrencyTypeId || previous[0].PaymentFrequencyId != rate.PaymentFrequencyId))
        { error = "Eski tutar sıfır veya tarife para birimi/dönemi farklı; güvenilir zam oranı belirlenemedi."; errorCode = "INVALID_INCREASE"; }
        try
        {
            if (error is null) message = CollectionSmsTemplate.Render(template, new Dictionary<string, string>
            {
                ["MusteriAdi"] = name ?? "Müşterimiz", ["AboneNo"] = contract.SubscriberCode ?? "",
                ["HizmetAdi"] = contract.Service ?? "", ["EskiTutar"] = old.ToString("N2", Turkish),
                ["YeniTutar"] = rate.Amount.Value.ToString("N2", Turkish),
                ["ZamOrani"] = percent?.ToString("0.########", Turkish) ?? "",
                ["ParaBirimi"] = currency ?? "", ["OdemeDonemi"] = frequency ?? "",
                ["GecerlilikTarihi"] = rate.EffectiveFrom.ToString("dd.MM.yyyy", Turkish)
            });
        }
        catch (InvalidOperationException ex) { error = ex.Message; errorCode = "INVALID_TEMPLATE"; }
        var notification = await db.Set<CollectionSmsNotification>().AsNoTracking().SingleOrDefaultAsync(x => x.RatePeriodId == rate.Id, ct);
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { contractId, rate.Id, recipientId, phone, message, template, old, rate.Amount, rate.EffectiveFrom, Simulation,
                notificationStatus = notification?.Status, attempts = notification?.AttemptCount })));
        var block = error;
        if (block is null && notification is not null && (notification.OldAmount != old
            || notification.NewAmount != rate.Amount || notification.EffectiveDate != rate.EffectiveFrom))
            block = "Zamın finansal bilgileri önceki bildirimden sonra değiştirilmiş; manuel inceleme gereklidir.";
        if (block is null && notification is not null && notification.Status != CollectionSmsStatus.Failed)
            block = notification.Status == CollectionSmsStatus.Unknown
                ? "Önceki gönderimin sonucu belirsiz. Servis kontrolü yapılmadan yeniden gönderilemez."
                : "Bu zam için SMS zaten kuyruğa alındı veya işlendi; mükerrer gönderim yapılmaz.";
        if (block is null && notification?.AttemptCount >= 10) block = "Bu bildirim için deneme sınırına ulaşıldı; servis incelemesi gereklidir.";
        return new(new(rate.Id, name, phone, message, rate.EffectiveFrom, old, rate.Amount.Value,
            percent, hash, Simulation, block is null, block), recipientId, template, errorCode);
    }

    private static CollectionSmsNotification CreateNotification(long contractId, long rateId, decimal old, decimal amount,
        decimal? percent, DateOnly effective) => new()
    {
        ContractId = contractId, RatePeriodId = rateId, OldAmount = old, NewAmount = amount,
        IncreasePercent = percent, EffectiveDate = effective, CreatedDate = DateTimeOffset.UtcNow
    };

    private void AddAttempt(CollectionSmsNotification n, Prepared? prepared, long actor, bool automatic, string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var invalid = prepared is null || prepared.ErrorCode is not null;
        var attempt = new CollectionSmsAttempt
        {
            Sequence = ++n.AttemptCount, RecipientCustomerId = prepared?.RecipientId,
            Phone = prepared?.Preview.Phone ?? "", Message = prepared?.Preview.Message ?? "",
            Template = prepared?.Template.Length <= 2000 ? prepared.Template : "",
            Reason = reason, IsAutomatic = automatic, IsSimulation = Simulation,
            Status = invalid ? CollectionSmsStatus.Failed : CollectionSmsStatus.Pending,
            ResultMessage = invalid ? prepared?.Preview.BlockReason ?? "Zam bildirimi hazırlanamadı." : "Gönderim bekleniyor.",
            ErrorCode = invalid ? prepared?.ErrorCode ?? "PREPARATION_FAILED" : null,
            CreatedDate = now, CreatedUser = actor, CompletedDate = invalid ? now : null
        };
        n.Attempts.Add(attempt);
        n.Status = attempt.Status;
        n.UpdatedDate = now;
    }
}
