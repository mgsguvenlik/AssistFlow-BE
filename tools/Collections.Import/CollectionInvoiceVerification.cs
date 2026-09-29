using Business.Services.Crm.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

internal static class CollectionInvoiceVerification
{
    // Invoked only through the AssistFlowTest-guarded setup command. Never mutates imported invoices.
    public static async Task RunAsync(AppDataContext db)
    {
        var template = await db.Set<CollectionInvoice>().AsNoTracking().FirstAsync();
        var actor = await db.Users.AsNoTracking().Where(x => !x.IsDeleted).Select(x => x.Id).FirstAsync();
        var marker = "K06-CHECK-" + Guid.NewGuid().ToString("N");
        var fixture = new CollectionInvoice { CustomerId = template.CustomerId, Type = "B", Number = marker,
            Date = new DateOnly(2026, 9, 29), Amount = 100m, CurrencyTypeId = template.CurrencyTypeId,
            CreatedDate = DateTimeOffset.UtcNow, CreatedUser = actor };
        db.Add(fixture);
        await db.SaveChangesAsync();
        var id = fixture.Id;
        db.ChangeTracker.Clear();
        var service = new CollectionInvoiceCommandService(db);
        var reader = new CollectionInvoiceService(db);
        var requests = new List<Guid>();
        CollectionInvoiceCommand Command(byte[] version) { var key = Guid.NewGuid(); requests.Add(key); return new() { RequestId = key, InvoiceRowVersion = version }; }
        async Task<CollectionInvoiceDetail> Detail()
        {
            var result = await reader.GetAsync(id, default);
            if (!result.IsSuccess || result.Data is null) throw new InvalidOperationException("Test faturası okunamadı.");
            return result.Data;
        }
        try
        {
            var initial = await Detail();
            var create = Command(initial.RowVersion); create.Amount = 40m; create.Date = new DateOnly(2026, 9, 29);
            var invalidActor = await service.ExecuteAsync(id, null, "PaymentCreate", create, 0, default);
            if (invalidActor.IsSuccess) throw new InvalidOperationException("Kullanıcı kontrolü başarısız.");
            var created = await service.ExecuteAsync(id, null, "PaymentCreate", create, actor, default);
            if (!created.IsSuccess || created.Data?.PaymentId is null) throw new InvalidOperationException(created.Message);
            var paymentId = created.Data.PaymentId.Value;
            var replay = await service.ExecuteAsync(id, null, "PaymentCreate", create, actor, default);
            if (!replay.IsSuccess || replay.Data?.Replayed != true) throw new InvalidOperationException("Tekrar güvenliği başarısız.");
            create.Amount = 41m;
            if ((int)(await service.ExecuteAsync(id, null, "PaymentCreate", create, actor, default)).StatusCode != 409)
                throw new InvalidOperationException("Aynı anahtar farklı içerik kontrolü başarısız.");
            var partial = await Detail();
            if (partial.Invoice.PaymentAmount != 40m || partial.Invoice.RemainingAmount != 60m) throw new InvalidOperationException("Kısmi ödeme hesabı başarısız.");
            var stale = Command(initial.RowVersion); stale.Description = "stale";
            if ((int)(await service.ExecuteAsync(id, null, "CommentUpdate", stale, actor, default)).StatusCode != 409)
                throw new InvalidOperationException("Eski sürüm kontrolü başarısız.");
            if ((int)(await service.ExecuteAsync(id, null, "InvoiceDelete", Command(partial.RowVersion), actor, default)).StatusCode != 409)
                throw new InvalidOperationException("Ödemeli fatura silme koruması başarısız.");
            var payment = await db.Set<CollectionInvoicePayment>().AsNoTracking().SingleAsync(x => x.Id == paymentId);
            var update = Command(partial.RowVersion); update.PaymentRowVersion = payment.RowVersion;
            update.Amount = 55m; update.Date = payment.Date; update.Description = "K06 doğrulama";
            if (!(await service.ExecuteAsync(id, paymentId, "PaymentUpdate", update, actor, default)).IsSuccess)
                throw new InvalidOperationException("Ödeme düzenleme başarısız.");
            var updated = await Detail();
            if (updated.Invoice.RemainingAmount != 45m) throw new InvalidOperationException("Düzenleme bakiyesi yanlış.");
            var paged = await reader.PaymentsAsync(id, new(), default);
            if (!paged.IsSuccess || paged.Data?.TotalCount != 1) throw new InvalidOperationException("Ödeme sayfalama başarısız.");
            payment = await db.Set<CollectionInvoicePayment>().AsNoTracking().SingleAsync(x => x.Id == paymentId);
            var remove = Command(updated.RowVersion); remove.PaymentRowVersion = payment.RowVersion;
            if (!(await service.ExecuteAsync(id, paymentId, "PaymentDelete", remove, actor, default)).IsSuccess)
                throw new InvalidOperationException("Ödeme silme başarısız.");
            if (await db.Set<CollectionInvoicePayment>().AnyAsync(x => x.Id == paymentId)) throw new InvalidOperationException("Fiziksel silme doğrulanamadı.");
            var afterDelete = await Detail();
            var comment = Command(afterDelete.RowVersion); comment.Description = "K06 açıklama doğrulama";
            if (!(await service.ExecuteAsync(id, null, "CommentUpdate", comment, actor, default)).IsSuccess)
                throw new InvalidOperationException("Açıklama güncelleme başarısız.");
            var commented = await Detail();
            if (commented.Comment != comment.Description || commented.Invoice.RemainingAmount != 100m) throw new InvalidOperationException("Açıklama/bakiye doğrulaması başarısız.");
            if (!(await service.ExecuteAsync(id, null, "InvoiceDelete", Command(commented.RowVersion), actor, default)).IsSuccess)
                throw new InvalidOperationException("Fatura silme başarısız.");
            if (await db.Set<CollectionInvoice>().AnyAsync(x => x.Id == id) || await db.Set<CollectionInvoiceOperation>().CountAsync(x => x.InvoiceId == id) != 5)
                throw new InvalidOperationException("Kalıcı işlem izi doğrulaması başarısız.");
            Console.WriteLine("K06 komut kontrolü başarılı: kısmi ödeme, düzenleme, fiziksel silme, açıklama, sürüm çakışması, tekrar güvenliği ve işlem izi.");
        }
        finally
        {
            db.ChangeTracker.Clear();
            await using var cleanup = await db.Database.BeginTransactionAsync();
            var current = await db.Set<CollectionInvoice>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (current is not null && (current.Number != marker || current.LegacyInvoiceFollowId != null))
                throw new InvalidOperationException("Test temizliği hedef doğrulaması başarısız.");
            await db.Set<CollectionInvoicePayment>().Where(x => x.InvoiceId == id).ExecuteDeleteAsync();
            await db.Set<CollectionInvoice>().Where(x => x.Id == id && x.Number == marker && x.LegacyInvoiceFollowId == null).ExecuteDeleteAsync();
            await db.Set<CollectionInvoiceOperation>().Where(x => x.InvoiceId == id && requests.Contains(x.RequestId)).ExecuteDeleteAsync();
            await cleanup.CommitAsync();
            Console.WriteLine("Yalnız geçici K06 doğrulama kaydı ve ona ait işlem izleri temizlendi; aktarılan faturalara dokunulmadı.");
        }
    }
}
