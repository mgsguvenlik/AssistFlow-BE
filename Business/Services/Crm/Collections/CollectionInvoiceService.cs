using Business.Interfaces;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionInvoiceService(AppDataContext db) : ICollectionInvoiceService
{
    private IQueryable<CollectionInvoice> Source
    {
        get
        {
            var customers = CollectionCustomerScopeQuery.Customers(db.Customers).Select(c => c.Id);
            return db.Set<CollectionInvoice>().AsNoTracking().Where(x => customers.Contains(x.CustomerId));
        }
    }

    private static IQueryable<CollectionInvoiceItem> Rows(IQueryable<CollectionInvoice> source) => source.Select(x =>
        new CollectionInvoiceItem(x.Id, x.CustomerId, x.Customer.SubscriberCompany, x.Customer.SubscriberCode,
            x.Type, x.Number, x.Date, x.Amount, x.CurrencyType.Code,
            x.Payments.Sum(p => (decimal?)p.Amount) ?? 0,
            x.Amount - (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0), x.ProjectCode));

    public async Task<ResponseModel<PagedResult<CollectionInvoiceItem>>> ListAsync(CollectionInvoiceQuery q, CancellationToken ct)
    {
        if (q.Page is < 1 or > 1000000 || q.PageSize is < 1 or > 100 || q.CustomerId <= 0 ||
            q.Type is not (null or "B" or "K") || q.Search?.Length > 200 || q.From > q.To)
            return ResponseModel<PagedResult<CollectionInvoiceItem>>.Fail("Fatura filtreleri veya sayfa bilgisi geçersiz.");
        var source = Source;
        if (q.CustomerId.HasValue) source = source.Where(x => x.CustomerId == q.CustomerId);
        if (q.Type is not null) source = source.Where(x => x.Type == q.Type);
        if (q.From.HasValue) source = source.Where(x => x.Date >= q.From);
        if (q.To.HasValue) source = source.Where(x => x.Date <= q.To);
        var search = q.Search?.Trim();
        if (!string.IsNullOrEmpty(search)) source = source.Where(x => (x.Number != null && x.Number.Contains(search)) ||
            (x.Customer.SubscriberCompany != null && x.Customer.SubscriberCompany.Contains(search)) ||
            (x.Customer.SubscriberCode != null && x.Customer.SubscriberCode.Contains(search)));
        if (q.Paid.HasValue) source = q.Paid.Value
            ? source.Where(x => x.Amount - (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0) <= 0)
            : source.Where(x => x.Amount - (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0) > 0);
        var count = await source.CountAsync(ct);
        var rows = await Rows(source.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)).ToListAsync(ct);
        return ResponseModel<PagedResult<CollectionInvoiceItem>>.Success(new(rows, count, q.Page, q.PageSize), "Kesilen faturalar getirildi.");
    }

    public async Task<ResponseModel<CollectionInvoiceDetail>> GetAsync(long id, CancellationToken ct)
    {
        var source = Source.Where(x => x.Id == id);
        var item = await source.Select(x => new CollectionInvoiceDetail(
            new CollectionInvoiceItem(x.Id, x.CustomerId, x.Customer.SubscriberCompany, x.Customer.SubscriberCode,
                x.Type, x.Number, x.Date, x.Amount, x.CurrencyType.Code,
                x.Payments.Sum(p => (decimal?)p.Amount) ?? 0,
                x.Amount - (x.Payments.Sum(p => (decimal?)p.Amount) ?? 0), x.ProjectCode),
            x.Comment, x.RowVersion, x.LegacyInvoiceFollowId)).SingleOrDefaultAsync(ct);
        return item is null ? ResponseModel<CollectionInvoiceDetail>.Fail("Fatura bulunamadı.", StatusCode.NotFound)
            : ResponseModel<CollectionInvoiceDetail>.Success(item, "Fatura detayı getirildi.");
    }

    public async Task<ResponseModel<PagedResult<CollectionInvoicePaymentItem>>> PaymentsAsync(long id, CollectionRateHistoryQuery q, CancellationToken ct)
    {
        if (q.Page is < 1 or > 1000000 || q.PageSize is < 1 or > 100)
            return ResponseModel<PagedResult<CollectionInvoicePaymentItem>>.Fail("Sayfa bilgisi geçersiz.");
        if (!await Source.AnyAsync(x => x.Id == id, ct))
            return ResponseModel<PagedResult<CollectionInvoicePaymentItem>>.Fail("Fatura bulunamadı.", StatusCode.NotFound);
        var source = db.Set<CollectionInvoicePayment>().AsNoTracking().Where(x => x.InvoiceId == id);
        var count = await source.CountAsync(ct);
        var rows = await source.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(x => new CollectionInvoicePaymentItem(x.Id, x.Date, x.Amount, x.Description, x.RowVersion, x.LegacyInvoiceFollowPaymentId)).ToListAsync(ct);
        return ResponseModel<PagedResult<CollectionInvoicePaymentItem>>.Success(new(rows, count, q.Page, q.PageSize), "Fatura ödemeleri getirildi.");
    }
}
