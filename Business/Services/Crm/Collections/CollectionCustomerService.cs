using Business.Interfaces;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionCustomerService(AppDataContext db) : ICollectionCustomerService
{
    private IQueryable<Customer> Customers => CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking());
    public async Task<ResponseModel<CollectionCustomerCard>> GetAsync(long id, CancellationToken ct)
    {
        var item = await Customers.Where(x => x.Id == id).Select(x => new CollectionCustomerCard(
            x.Id, x.SubscriberCode, x.SubscriberCompany, x.SubscriberAddress, x.City, x.District,
            x.ContactName1, x.Phone1, x.Email1, x.ContactName2, x.Phone2, x.Email2, x.Note,
            x.CustomerType!.Name, x.CustomerGroupId, x.CustomerGroup!.GroupName,
            db.Set<CollectionGroupParent>().Any(p => p.CustomerId == x.Id),
            CollectionCustomerClassification.GroupOperationalTypeCodes.Contains(x.CustomerType.Code))).SingleOrDefaultAsync(ct);
        return item is null ? ResponseModel<CollectionCustomerCard>.Fail("Tahsilat kapsamında müşteri bulunamadı.", StatusCode.NotFound)
            : ResponseModel<CollectionCustomerCard>.Success(item, "Müşteri bilgileri getirildi.");
    }
    private Task<bool> Exists(long id, CancellationToken ct) => Customers.AnyAsync(x => x.Id == id, ct);
    private static bool ValidPage(CollectionRateHistoryQuery q) => q.Page is >= 1 and <= 1000000 && q.PageSize is >= 1 and <= 100;

    public async Task<ResponseModel<PagedResult<CollectionCustomerNoteItem>>> NotesAsync(long id, CollectionRateHistoryQuery q, CancellationToken ct)
    {
        if (!ValidPage(q)) return ResponseModel<PagedResult<CollectionCustomerNoteItem>>.Fail("Geçersiz sayfa bilgisi.");
        if (!await Exists(id, ct)) return ResponseModel<PagedResult<CollectionCustomerNoteItem>>.Fail("Tahsilat kapsamında müşteri bulunamadı.", StatusCode.NotFound);
        var source = db.Set<CollectionCustomerNote>().AsNoTracking().Where(x => x.CustomerId == id && !x.IsDeleted);
        var count = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.CreatedDate).ThenByDescending(x => x.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new CollectionCustomerNoteItem(x.Id, x.Text, x.CreatedDate, x.UpdatedDate,
                x.CreatedUser, x.UpdatedUser, x.RowVersion, x.LegacyCreatedBy, x.LegacyModifiedBy, x.LegacyCreatedOn, x.LegacyModifiedOn))
            .ToListAsync(ct);
        return ResponseModel<PagedResult<CollectionCustomerNoteItem>>.Success(new(rows, count, q.Page, q.PageSize), "Not geçmişi getirildi.");
    }
    public async Task<ResponseModel<PagedResult<CollectionCustomerPaymentItem>>> PaymentsAsync(long id, CollectionRateHistoryQuery q, CancellationToken ct)
    {
        if (!ValidPage(q)) return ResponseModel<PagedResult<CollectionCustomerPaymentItem>>.Fail("Geçersiz sayfa bilgisi.");
        if (!await Exists(id, ct)) return ResponseModel<PagedResult<CollectionCustomerPaymentItem>>.Fail("Tahsilat kapsamında müşteri bulunamadı.", StatusCode.NotFound);
        var source = db.Set<CollectionPayment>().AsNoTracking().Where(x => x.Contract.CustomerId == id && !x.Contract.IsDeleted);
        var count = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.PaymentDate).ThenByDescending(x => x.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new CollectionCustomerPaymentItem(x.Id, x.ContractId, x.Contract.ServiceType.Name,
                x.Period, x.PaymentDate, x.Amount, x.CurrencyType.Code, x.Description, x.IsFree)).ToListAsync(ct);
        return ResponseModel<PagedResult<CollectionCustomerPaymentItem>>.Success(new(rows, count, q.Page, q.PageSize), "Ödeme hareketleri getirildi.");
    }
    public async Task<ResponseModel<long>> AddNoteAsync(long id, CollectionCustomerNoteCreate command, long actor, CancellationToken ct)
    {
        if (actor <= 0 || string.IsNullOrWhiteSpace(command.Text) || command.Text.Length > 10000)
            return ResponseModel<long>.Fail("Geçerli kullanıcı ve 1–10000 karakterlik not girin.");
        if (!await Exists(id, ct)) return ResponseModel<long>.Fail("Tahsilat kapsamında müşteri bulunamadı.", StatusCode.NotFound);
        var note = new CollectionCustomerNote { CustomerId = id, Text = command.Text.Trim(), CreatedDate = DateTimeOffset.UtcNow, CreatedUser = actor };
        db.Add(note);
        await db.SaveChangesAsync(ct);
        return ResponseModel<long>.Success(note.Id, "Not eklendi.");
    }
    public async Task<ResponseModel<long>> ChangeNoteAsync(long id, long noteId, CollectionCustomerNoteChange command, bool delete, long actor, CancellationToken ct)
    {
        if (actor <= 0 || command.RowVersion?.Length != 8 || !delete && (string.IsNullOrWhiteSpace(command.Text) || command.Text.Length > 10000))
            return ResponseModel<long>.Fail("Not metni veya kayıt sürümü geçersiz.");
        if (!await Exists(id, ct)) return ResponseModel<long>.Fail("Tahsilat kapsamında müşteri bulunamadı.", StatusCode.NotFound);
        var note = await db.Set<CollectionCustomerNote>().SingleOrDefaultAsync(x => x.Id == noteId && x.CustomerId == id && !x.IsDeleted, ct);
        if (note is null) return ResponseModel<long>.Fail("Not bulunamadı.", StatusCode.NotFound);
        db.Entry(note).Property(x => x.RowVersion).OriginalValue = command.RowVersion;
        if (delete) note.IsDeleted = true;
        else note.Text = command.Text!.Trim();
        note.UpdatedDate = DateTimeOffset.UtcNow;
        note.UpdatedUser = actor;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            return ResponseModel<long>.Fail("Not başka bir kullanıcı tarafından değiştirildi. Listeyi yenileyip tekrar deneyin.", StatusCode.Conflict);
        }
        return ResponseModel<long>.Success(note.Id, delete ? "Not silindi." : "Not güncellendi.");
    }
}
